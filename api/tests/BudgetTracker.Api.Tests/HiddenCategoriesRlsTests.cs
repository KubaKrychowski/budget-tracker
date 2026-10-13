using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Row-level security na <c>HiddenCategories</c> sprawdzana W BAZIE: połączenie idzie rolą <c>budget_app</c>, a odczyty mają
/// <c>IgnoreQueryFilters()</c>, więc filtr Owner nie może niczego ukryć. Ukrycie jednego konta nie może być widoczne dla innego
/// ani — co ważniejsze — nie może ukryć kategorii wspólnej wszystkim. Wzorzec: <c>StrategiesRlsTests</c>.
///
/// Wymaga `docker compose up -d db` oraz ról `budget_app` i `budget_jobs` (patrz api/db/setup-rls-roles.sql).
/// </summary>
public sealed class HiddenCategoriesRlsTests : IAsyncLifetime
{
    private const string OwnerConnection =
        "Host=localhost;Port=5432;Database=budgettracker_hiddenrls_test;Username=budget;Password=budget_dev_only";

    private const string AppConnection =
        "Host=localhost;Port=5432;Database=budgettracker_hiddenrls_test;Username=budget_app;Password=budget_app_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();
    private int _sharedId;

    public async Task InitializeAsync()
    {
        await TestDatabase.ResetAsync(OwnerConnection);

        await using var owner = OwnerContext();
        await owner.Database.MigrateAsync();

        await owner.Database.ExecuteSqlRawAsync(
            """
            GRANT USAGE ON SCHEMA public TO budget_app, budget_jobs;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO budget_app, budget_jobs;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO budget_app, budget_jobs;
            """);

        var shared = new Category("Paliwo");
        owner.Categories.Add(shared);
        await owner.SaveChangesAsync();
        _sharedId = shared.Id;

        owner.HiddenCategories.Add(new HiddenCategory(_alice, _sharedId));
        await owner.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await TestDatabase.DropAsync(OwnerConnection);

    private AppDbContext OwnerContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(OwnerConnection)
        .AddInterceptors(new SoftDeleteInterceptor(_clock))
        .Options);

    private AppDbContext AppAs(Guid? userId)
    {
        ICurrentUserAccessor accessor = new TestUser(userId);
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(AppConnection)
            .AddInterceptors(new RlsSessionInterceptor(accessor), new SoftDeleteInterceptor(_clock))
            .Options);
    }

    private static async Task<int> SeenAsync(AppDbContext db) =>
        await db.HiddenCategories.IgnoreQueryFilters().CountAsync();

    [Fact]
    public async Task A_user_sees_only_their_own_hidden_categories()
    {
        await using var alice = AppAs(_alice);
        await using var bob = AppAs(_bob);

        Assert.Equal(1, await SeenAsync(alice));
        // Łapie ukrycie wspólnej kategorii wszystkim: drugie konto nie ma widzieć ukrycia pierwszego.
        Assert.Equal(0, await SeenAsync(bob));
    }

    [Fact]
    public async Task Without_a_user_in_the_session_no_hidden_category_is_visible()
    {
        await using var anonymous = AppAs(null);

        Assert.Equal(0, await SeenAsync(anonymous));
    }

    [Fact]
    public async Task A_user_cannot_hide_a_category_on_behalf_of_someone_else()
    {
        await using var bob = AppAs(_bob);

        bob.HiddenCategories.Add(new HiddenCategory(_alice, _sharedId + 1));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => bob.SaveChangesAsync());

        Assert.Equal("42501", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    private sealed class TestUser(Guid? userId) : ICurrentUserAccessor
    {
        public Guid UserId => userId ?? throw new InvalidOperationException("Brak użytkownika w teście.");

        public Guid? UserIdOrNull => userId;
    }
}
