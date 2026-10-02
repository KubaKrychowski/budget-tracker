using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Row-level security na <c>Categories</c> sprawdzana W BAZIE, nie w EF: połączenie idzie rolą <c>budget_app</c>
/// (zwykła rola, RLS jej dotyczy), a odczyty mają <c>IgnoreQueryFilters()</c>, więc filtr Owner nie może niczego ukryć.
///
/// <para>
/// ⚠️ Reszta testów łączy się jako <c>budget</c> (superuser), który omija RLS zawsze — zielone testy handlerów niczego
/// więc nie mówią o politykach. Baza powstaje z PRAWDZIWYCH migracji (<c>MigrateAsync</c>), bo polityki są SQL-em
/// w migracji, a <c>EnsureCreated</c> ich nie zakłada. Wzorzec: <c>CategoryRulesRlsTests</c>.
/// </para>
///
/// Wymaga `docker compose up -d db` oraz ról `budget_app` i `budget_jobs` (patrz api/db/setup-rls-roles.sql).
/// </summary>
public sealed class CategoriesRlsTests : IAsyncLifetime
{
    private const string OwnerConnection =
        "Host=localhost;Port=5432;Database=budgettracker_categoriesrls_test;Username=budget;Password=budget_dev_only";

    private const string AppConnection =
        "Host=localhost;Port=5432;Database=budgettracker_categoriesrls_test;Username=budget_app;Password=budget_app_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();

    private Guid _shared;
    private Guid _aliceCategory;
    private Guid _bobCategory;

    public async Task InitializeAsync()
    {
        await TestDatabase.ResetAsync(OwnerConnection);

        await using var owner = OwnerContext();
        await owner.Database.MigrateAsync();

        // Uprawnienia tabel są POZA migracjami (api/db/grant-app-privileges.sql, raz na bazę) — tu jego zawartość.
        await owner.Database.ExecuteSqlRawAsync(
            """
            GRANT USAGE ON SCHEMA public TO budget_app, budget_jobs;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO budget_app, budget_jobs;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO budget_app, budget_jobs;
            """);

        var shared = new Category("Wspolna");
        var alice = new Category("Alicji", CategoryType.Expense, _alice);
        var bob = new Category("Boba", CategoryType.Income, _bob);
        owner.Categories.AddRange(shared, alice, bob);
        await owner.SaveChangesAsync();
        (_shared, _aliceCategory, _bobCategory) = (shared.BusinessId, alice.BusinessId, bob.BusinessId);
    }

    public async Task DisposeAsync() => await TestDatabase.DropAsync(OwnerConnection);

    private AppDbContext OwnerContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(OwnerConnection)
        .AddInterceptors(new SoftDeleteInterceptor(_clock))
        .Options);

    /// <summary>Kontekst appki: rola <c>budget_app</c> i zmienna sesyjna ustawiana przez <see cref="RlsSessionInterceptor"/>.</summary>
    private AppDbContext AppAs(Guid? userId)
    {
        ICurrentUserAccessor accessor = new TestUser(userId);
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(AppConnection)
            .AddInterceptors(new RlsSessionInterceptor(accessor), new SoftDeleteInterceptor(_clock))
            .Options);
    }

    private static async Task<List<string>> NamesSeenAsync(AppDbContext db) =>
        await db.Categories.IgnoreQueryFilters().OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();

    [Fact]
    public async Task A_user_sees_own_and_shared_categories_but_never_another_users()
    {
        await using var alice = AppAs(_alice);
        await using var bob = AppAs(_bob);

        Assert.Equal(["Alicji", "Wspolna"], await NamesSeenAsync(alice));
        Assert.Equal(["Boba", "Wspolna"], await NamesSeenAsync(bob));
    }

    [Fact]
    public async Task Without_a_user_in_the_session_only_shared_categories_are_visible()
    {
        await using var anonymous = AppAs(null);

        Assert.Equal(["Wspolna"], await NamesSeenAsync(anonymous));
    }

    [Fact]
    public async Task A_user_can_add_a_category_of_their_own()
    {
        await using var alice = AppAs(_alice);

        alice.Categories.Add(new Category("Nowa", CategoryType.Expense, _alice));
        await alice.SaveChangesAsync();

        Assert.Equal(["Alicji", "Nowa", "Wspolna"], await NamesSeenAsync(alice));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_user_cannot_add_a_category_for_someone_else_or_a_shared_one(bool shared)
    {
        await using var alice = AppAs(_alice);

        alice.Categories.Add(new Category("Podrzucona", CategoryType.Expense, shared ? Category.SharedUserId : _bob));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => alice.SaveChangesAsync());

        // 42501 = insufficient_privilege: „new row violates row-level security policy".
        Assert.Equal("42501", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task A_user_cannot_change_or_delete_a_shared_or_foreign_category()
    {
        await using var alice = AppAs(_alice);

        foreach (var id in new[] { _shared, _bobCategory })
        {
            var updated = await alice.Categories.IgnoreQueryFilters().Where(c => c.BusinessId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, "Przejęta"));
            var deleted = await alice.Categories.IgnoreQueryFilters().Where(c => c.BusinessId == id)
                .ExecuteDeleteAsync();

            Assert.Equal(0, updated);
            Assert.Equal(0, deleted);
        }

        await using var owner = OwnerContext();
        Assert.Equal(["Alicji", "Boba", "Wspolna"], await owner.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync());
    }

    [Fact]
    public async Task A_user_can_change_and_delete_their_own_category()
    {
        await using var alice = AppAs(_alice);

        var updated = await alice.Categories.IgnoreQueryFilters().Where(c => c.BusinessId == _aliceCategory)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, "Zmieniona"));
        var deleted = await alice.Categories.IgnoreQueryFilters().Where(c => c.BusinessId == _aliceCategory)
            .ExecuteDeleteAsync();

        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
    }

    [Fact]
    public async Task The_jobs_role_can_write_shared_categories_because_it_bypasses_rls()
    {
        // Tak seed kategorii bazowych zapisuje je na starcie (Program.cs): budget_app wchodzi w budget_jobs na czas jednej transakcji.
        await using var app = AppAs(null);
        await using var transaction = await app.Database.BeginTransactionAsync();
        await app.Database.ExecuteSqlRawAsync("SET LOCAL ROLE budget_jobs");

        app.Categories.Add(new Category("Z seeda"));
        await app.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var alice = AppAs(_alice);
        Assert.Contains("Z seeda", await NamesSeenAsync(alice));
    }

    private sealed class TestUser(Guid? userId) : ICurrentUserAccessor
    {
        public Guid UserId => userId ?? throw new InvalidOperationException("Brak użytkownika w teście.");

        public Guid? UserIdOrNull => userId;
    }
}
