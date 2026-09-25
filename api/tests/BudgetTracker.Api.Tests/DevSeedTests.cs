using BudgetTracker.Api.Features.Admin.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Seedy deweloperskie startują przy każdym uruchomieniu API w Development, gdy nie ma transakcji. Konta seedu mają stałe
/// <c>BusinessId</c> i NIE należą do żadnego użytkownika, więc przeżywają usunięcie jego danych — kolejny start seeda nie
/// może ich założyć drugi raz.
///
/// Wymaga `docker compose up -d db` oraz roli `budget_jobs` (patrz api/db/setup-rls-roles.sql).
/// </summary>
public sealed class DevSeedTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_devseed_test;Username=budget;Password=budget_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero));

    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();
        await BaselineSeed.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await TestDatabase.DropAsync(TestConnection);
    }

    private async Task DeleteOwnerDataAsync(Guid ownerId)
    {
        // To samo, co robi „Usuń” na ekranie administratora: trwale kasuje dane właściciela, konta seedu zostają.
        var service = new OwnerDataService(_db, TestBlobs.Client());
        await service.AsSystemAsync(() => service.DeleteAsync(ownerId, default), default);
    }

    [Fact]
    public async Task Dev_seed_runs_again_after_the_owners_data_was_deleted_without_duplicating_accounts()
    {
        // ⚠️ Prawdziwy błąd: po usunięciu danych dev-użytkownika strażnik „są transakcje” widzi pustą bazę i seed zakłada
        // te same trzy konta drugi raz — API nie wstaje na `IX_Accounts_BusinessId`.
        await DevSeed.SeedAsync(_db, _clock);
        Assert.Equal(3, await _db.Accounts.CountAsync());

        await DeleteOwnerDataAsync(DeterministicGuid.For("dev:user:owner"));
        Assert.Empty(await _db.Transactions.ToListAsync());

        await DevSeed.SeedAsync(_db, _clock);

        Assert.Equal(3, await _db.Accounts.CountAsync());
        Assert.NotEmpty(await _db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Dev_seed_brings_back_a_seed_account_that_was_deleted_in_the_app()
    {
        await DevSeed.SeedAsync(_db, _clock);
        _db.Accounts.Remove(await _db.Accounts.FirstAsync());
        await _db.SaveChangesAsync();
        await DeleteOwnerDataAsync(DeterministicGuid.For("dev:user:owner"));

        await DevSeed.SeedAsync(_db, _clock);

        // Skasowane konto nadal blokuje unikalny BusinessId, a transakcje seeda muszą mieć do czego się odwołać.
        Assert.Equal(3, await _db.Accounts.CountAsync());
    }
}
