using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Row-level security na <c>Receipts</c> sprawdzana W BAZIE, nie w EF: połączenie idzie rolą <c>budget_app</c>, a odczyty
/// mają <c>IgnoreQueryFilters()</c>, więc filtr Owner nie może niczego ukryć. Baza powstaje z PRAWDZIWYCH migracji, bo
/// polityka jest SQL-em w migracji. Wzorzec: <c>StrategiesRlsTests</c>.
///
/// Wymaga `docker compose up -d db` oraz ról `budget_app` i `budget_jobs` (patrz api/db/setup-rls-roles.sql).
/// </summary>
public sealed class ReceiptsRlsTests : IAsyncLifetime
{
    private const string OwnerConnection =
        "Host=localhost;Port=5432;Database=budgettracker_receiptsrls_test;Username=budget;Password=budget_dev_only";

    private const string AppConnection =
        "Host=localhost;Port=5432;Database=budgettracker_receiptsrls_test;Username=budget_app;Password=budget_app_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();

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

        owner.Receipts.AddRange(NewReceipt(_alice, "alicji.jpg"), NewReceipt(_bob, "boba.jpg"));
        await owner.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await TestDatabase.DropAsync(OwnerConnection);

    private Receipt NewReceipt(Guid userId, string fileName) =>
        new(userId, $"receipts/{Guid.NewGuid():N}.jpg", fileName, "image/jpeg", 10, _clock.GetUtcNow());

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

    private static async Task<List<string>> FilesSeenAsync(AppDbContext db) =>
        await db.Receipts.IgnoreQueryFilters().OrderBy(r => r.FileName).Select(r => r.FileName).ToListAsync();

    [Fact]
    public async Task A_user_sees_only_their_own_receipts()
    {
        await using var alice = AppAs(_alice);
        await using var bob = AppAs(_bob);

        Assert.Equal(["alicji.jpg"], await FilesSeenAsync(alice));
        Assert.Equal(["boba.jpg"], await FilesSeenAsync(bob));
    }

    [Fact]
    public async Task Without_a_user_in_the_session_no_receipt_is_visible()
    {
        await using var anonymous = AppAs(null);

        Assert.Empty(await FilesSeenAsync(anonymous));
    }

    [Fact]
    public async Task A_user_cannot_add_a_receipt_for_someone_else()
    {
        await using var alice = AppAs(_alice);

        alice.Receipts.Add(NewReceipt(_bob, "podrzucony.jpg"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => alice.SaveChangesAsync());

        Assert.Equal("42501", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    private sealed class TestUser(Guid? userId) : ICurrentUserAccessor
    {
        public Guid UserId => userId ?? throw new InvalidOperationException("Brak użytkownika w teście.");

        public Guid? UserIdOrNull => userId;
    }
}
