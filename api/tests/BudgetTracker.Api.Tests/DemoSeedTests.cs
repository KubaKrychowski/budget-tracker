using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Dane demonstracyjne pod materiały publiczne (issue #12).
///
/// <para>
/// Te testy pilnują rzeczy, których nie widać w kodzie, a które psują ZRZUTY EKRANU —
/// czyli jedyny produkt tego seeda. Zrzut z błędnymi danymi trafia na stronę publiczną
/// i zostaje tam w cache'ach na zawsze, więc lepiej złapać to tutaj niż okiem.
/// </para>
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class DemoSeedTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_demoseed_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś" w POŁOWIE miesiąca — inaczej pułapka z datami w przyszłości nie odpala.</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero));

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

        // Seed korzysta z taksonomii produkcyjnej, tak samo jak DevSeed.
        await BaselineSeed.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Never_creates_transactions_dated_in_the_future()
    {
        // ⚠️ To był prawdziwy błąd, nie hipoteza: wypłata stała na 10. dniu miesiąca, a „dziś"
        // było 8., więc bieżący miesiąc dostawał transakcje z przyszłości. Dashboard liczy okres
        // kończący się dzisiaj i je pomijał, ale tabele już nie — zrzuty pokazywały wtedy dwie
        // różne prawdy o tym samym miesiącu.
        await DemoSeed.SeedAsync(_db, _clock);

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime.Date);
        var future = await _db.Transactions.Where(t => t.Date > today).ToListAsync();

        Assert.Empty(future);
    }

    [Fact]
    public async Task Produces_a_closed_month_where_the_goal_survived_a_one_off_expense()
    {
        // Bez takiego miesiąca ekran oszczędności nie ma z czego postawić zdania o odporności
        // celu — a to jest JEDYNY powód, dla którego ten zrzut jest na landingu.
        await DemoSeed.SeedAsync(_db, _clock);

        var goal = await _db.SavingsGoals.SingleAsync();
        var oneOffIds = await _db.EpisodicOrders.Select(o => o.TransactionBusinessId).ToListAsync();
        var oneOff = await _db.Transactions
            .Where(t => oneOffIds.Contains(t.BusinessId) && t.Amount < 0)
            .ToListAsync();
        Assert.NotEmpty(oneOff);

        var savingsCategoryId = await _db.Categories
            .Where(c => c.Name == "Oszczędności").Select(c => c.Id).SingleAsync();

        var proofMonths = oneOff
            .Select(t => new DateOnly(t.Date.Year, t.Date.Month, 1))
            .Distinct()
            .Where(month => _db.Transactions
                .Where(t => t.CategoryId == savingsCategoryId && t.Amount < 0)
                .Where(t => t.Date >= month && t.Date < month.AddMonths(1))
                .Sum(t => -t.Amount) >= goal.Amount)
            .ToList();

        Assert.NotEmpty(proofMonths);
    }

    [Fact]
    public async Task Uses_no_real_company_names()
    {
        // ⚠️ Sedno issue #12. Test jest z natury niepełny — nie da się sprawdzić „czy ta nazwa
        // istnieje na świecie". Pilnuje więc konkretnego regresu: powrotu marek z DevSeed,
        // bo najprostszą (i kuszącą) zmianą tego pliku jest skopiowanie tamtej listy.
        // Ręczny przegląd nazw zostaje obowiązkiem człowieka, tak jak mówi issue.
        await DemoSeed.SeedAsync(_db, _clock);

        string[] realBrands =
        [
            "BIEDRONKA", "LIDL", "ZABKA", "ORLEN", "SHELL", "ORANGE", "TAURON",
            "LUXMED", "GEMINI", "DR MAX", "MACZFIT", "DIETLY", "KAUFLAND", "LEWIATAN",
        ];

        var descriptions = await _db.Transactions.Select(t => t.Description).ToListAsync();

        foreach (var brand in realBrands)
        {
            Assert.DoesNotContain(descriptions, d => d.Contains(brand, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Runs_again_after_the_owners_data_was_deleted_without_duplicating_accounts()
    {
        // ⚠️ To samo, co w DevSeed: konta demo mają stałe BusinessId i przeżywają usunięcie danych właściciela,
        // a strażnik „są transakcje” widzi wtedy pustą bazę.
        await DemoSeed.SeedAsync(_db, _clock);
        var accountsBefore = await _db.Accounts.CountAsync();

        var service = new BudgetTracker.Api.Features.Admin.Services.OwnerDataService(_db, TestBlobs.Client());
        await service.AsSystemAsync(
            () => service.DeleteAsync(DeterministicGuid.For("demo:user:owner"), default), default);
        Assert.Empty(await _db.Transactions.ToListAsync());

        await DemoSeed.SeedAsync(_db, _clock);

        Assert.Equal(accountsBefore, await _db.Accounts.CountAsync());
        Assert.NotEmpty(await _db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Refuses_to_touch_a_database_that_already_has_transactions()
    {
        // Trzeci z trzech zamków (obok Development i flagi `Demo:Seed`). Ten jest najważniejszy:
        // chroni bazę z prawdziwym wyciągiem przed dosypaniem do niej danych demo.
        _db.Transactions.Add(new Transaction(
            new DateOnly(2026, 1, 5),
            -10m,
            "ISTNIEJACA TRANSAKCJA",
            _clock.GetUtcNow(),
            TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        await DemoSeed.SeedAsync(_db, _clock);

        Assert.Equal(1, await _db.Transactions.CountAsync());
        Assert.Empty(await _db.SavingsGoals.ToListAsync());
    }
}
