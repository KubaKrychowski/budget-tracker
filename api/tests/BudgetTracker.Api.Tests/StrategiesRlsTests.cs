using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Row-level security na <c>Strategies</c> sprawdzana W BAZIE, nie w EF: połączenie idzie rolą <c>budget_app</c>
/// (zwykła rola, RLS jej dotyczy), a odczyty mają <c>IgnoreQueryFilters()</c>, więc filtr Owner nie może niczego ukryć.
///
/// <para>
/// ⚠️ Strategia niesie salda i kredyt — dane osobowe — więc wyciek między kontami byłby poważniejszy niż przy kategoriach.
/// Baza powstaje z PRAWDZIWYCH migracji (<c>MigrateAsync</c>), bo polityka jest SQL-em w migracji, a <c>EnsureCreated</c>
/// jej nie zakłada. Wzorzec: <c>CategoriesRlsTests</c>.
/// </para>
///
/// Wymaga `docker compose up -d db` oraz ról `budget_app` i `budget_jobs` (patrz api/db/setup-rls-roles.sql).
/// </summary>
public sealed class StrategiesRlsTests : IAsyncLifetime
{
    private const string OwnerConnection =
        "Host=localhost;Port=5432;Database=budgettracker_strategiesrls_test;Username=budget;Password=budget_dev_only";

    private const string AppConnection =
        "Host=localhost;Port=5432;Database=budgettracker_strategiesrls_test;Username=budget_app;Password=budget_app_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();
    private readonly Guid _budget = Guid.CreateVersion7();

    private Guid _aliceStrategy;
    private Guid _bobStrategy;

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

        var alice = new Strategy(_budget, "Alicji", new DateOnly(2026, 10, 1), 1_000m, 24, _clock.GetUtcNow(), _alice);
        var bob = new Strategy(_budget, "Boba", new DateOnly(2026, 10, 1), 2_000m, 24, _clock.GetUtcNow(), _bob);
        owner.Strategies.AddRange(alice, bob);
        await owner.SaveChangesAsync();
        (_aliceStrategy, _bobStrategy) = (alice.BusinessId, bob.BusinessId);
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

    private static async Task<List<string>> NamesSeenAsync(AppDbContext db) =>
        await db.Strategies.IgnoreQueryFilters().OrderBy(s => s.Name).Select(s => s.Name).ToListAsync();

    [Fact]
    public async Task A_user_sees_only_their_own_strategies()
    {
        await using var alice = AppAs(_alice);
        await using var bob = AppAs(_bob);

        Assert.Equal(["Alicji"], await NamesSeenAsync(alice));
        Assert.Equal(["Boba"], await NamesSeenAsync(bob));
    }

    [Fact]
    public async Task Without_a_user_in_the_session_no_strategy_is_visible()
    {
        await using var anonymous = AppAs(null);

        Assert.Empty(await NamesSeenAsync(anonymous));
    }

    [Fact]
    public async Task A_user_cannot_add_a_strategy_for_someone_else()
    {
        await using var alice = AppAs(_alice);

        alice.Strategies.Add(new Strategy(_budget, "Podrzucona", new DateOnly(2026, 10, 1), 0m, 24, _clock.GetUtcNow(), _bob));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => alice.SaveChangesAsync());

        Assert.Equal("42501", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task A_user_cannot_change_or_delete_a_foreign_strategy()
    {
        await using var alice = AppAs(_alice);

        var updated = await alice.Strategies.IgnoreQueryFilters().Where(s => s.BusinessId == _bobStrategy)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Przejęta"));
        var deleted = await alice.Strategies.IgnoreQueryFilters().Where(s => s.BusinessId == _bobStrategy)
            .ExecuteDeleteAsync();

        Assert.Equal((0, 0), (updated, deleted));
        await using var owner = OwnerContext();
        Assert.Equal("Boba", (await owner.Strategies.SingleAsync(s => s.BusinessId == _bobStrategy)).Name);
    }

    [Fact]
    public async Task A_user_can_save_a_graph_into_their_own_strategy()
    {
        await using var alice = AppAs(_alice);
        var own = await alice.Strategies.SingleAsync(s => s.BusinessId == _aliceStrategy);

        own.Replace("Alicji", new DateOnly(2026, 10, 1), 500m, 12,
            [new StrategyNode("n1", StrategyNodeType.Surplus, string.Empty, 0, 0, null, 700m, null, null, null, null, null, null)],
            [], _clock.GetUtcNow());
        await alice.SaveChangesAsync();

        await using var again = AppAs(_alice);
        Assert.Single((await again.Strategies.SingleAsync(s => s.BusinessId == _aliceStrategy)).Nodes);
    }

    private sealed class TestUser(Guid? userId) : ICurrentUserAccessor
    {
        public Guid UserId => userId ?? throw new InvalidOperationException("Brak użytkownika w teście.");

        public Guid? UserIdOrNull => userId;
    }
}
