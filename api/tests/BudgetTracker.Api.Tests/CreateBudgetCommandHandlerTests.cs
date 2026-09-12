using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Commands;
using BudgetTracker.Api.Features.Budgets.Consts;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Krok 1 kreatora budżetu („Dane podstawowe"): nazwa, waluta i bilans początkowy.
///
/// Makieta NIE pyta o miesiąc, choć budżet jest miesięczny — testy pilnują, że serwer
/// ustala go sam. Pilnują też, że KILKA budżetów na ten sam miesiąc jest dozwolonych:
/// budżet to zbiór zasad, na których pracują procesy wokół transakcji, więc porównywanie
/// wariantów tego samego okresu jest normalnym scenariuszem.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class CreateBudgetCommandHandlerTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_budgets_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 17, 8, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        // Interceptor tez w tescie — tak samo jak w produkcji. Odpowiada wylacznie za zamiane
        // fizycznego kasowania na logiczne; BusinessId nadaje sobie sama encja.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private CreateBudgetCommandHandler Handler() => new(_db, _clock);

    [Fact]
    public async Task Creates_a_budget_for_the_current_month()
    {
        var (budget, error) = await Handler().HandleAsync(new CreateBudgetRequestDto("Wrzesień", "PLN", 0m), default);

        Assert.Equal(CreateBudgetError.None, error);
        Assert.NotNull(budget);
        // Miesiac bierzemy z zegara, nie z DateTime.Now — inaczej test zalezalby od maszyny.
        Assert.Equal(new DateOnly(2026, 9, 1), budget!.Month);
        Assert.Equal("PLN", budget.Currency);
        Assert.Equal(1, await _db.Budgets.CountAsync());
    }

    [Fact]
    public async Task Trims_the_name_so_spaces_alone_do_not_pass_for_a_name()
    {
        var (budget, _) = await Handler().HandleAsync(new CreateBudgetRequestDto("  Domowy  ", "PLN", 0m), default);

        Assert.Equal("Domowy", budget!.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_an_empty_name(string name)
    {
        var (budget, error) = await Handler().HandleAsync(new CreateBudgetRequestDto(name, "PLN", 0m), default);

        Assert.Equal(CreateBudgetError.NameRequired, error);
        Assert.Null(budget);
        Assert.Empty(await _db.Budgets.ToListAsync());
    }

    [Fact]
    public async Task Accepts_the_currency_code_regardless_of_case()
    {
        var (budget, error) = await Handler().HandleAsync(new CreateBudgetRequestDto("Domowy", "pln", 0m), default);

        Assert.Equal(CreateBudgetError.None, error);
        Assert.Equal("PLN", budget!.Currency);
    }

    [Fact]
    public async Task Rejects_a_currency_we_cannot_handle()
    {
        // Waluta musi być w słowniku walut w bazie — wyciagi, ktore czytamy, sa zlotowkowe,
        // wiec obca waluta wymagalaby przeliczen, ktorych nie ma.
        var (budget, error) = await Handler().HandleAsync(new CreateBudgetRequestDto("Wakacje", "EUR", 0m), default);

        Assert.Equal(CreateBudgetError.UnsupportedCurrency, error);
        Assert.Null(budget);
    }

    [Fact]
    public async Task Allows_several_budgets_in_the_same_month()
    {
        // Budzet jest bytem ABSTRAKCYJNYM — zbiorem zasad, na ktorych pracuja procesy wokol
        // zaimportowanych transakcji — a import wskazuje, na ktory budzet naliczac. Kilka
        // budzetow na ten sam miesiac to normalny scenariusz: porownywanie wariantow tych
        // samych danych. Rozroznia je nazwa, nie okres.
        await Handler().HandleAsync(new CreateBudgetRequestDto("Wariant ostrozny", "PLN", 0m), default);

        var (budget, error) = await Handler().HandleAsync(
            new CreateBudgetRequestDto("Wariant optymistyczny", "PLN", 0m), default);

        Assert.Equal(CreateBudgetError.None, error);
        Assert.NotNull(budget);
        Assert.Equal(new DateOnly(2026, 9, 1), budget!.Month);
        Assert.Equal(2, await _db.Budgets.CountAsync());
    }

    [Fact]
    public async Task Allows_a_budget_for_a_different_month()
    {
        await Handler().HandleAsync(new CreateBudgetRequestDto("Wrzesień", "PLN", 0m), default);

        _clock.Advance(TimeSpan.FromDays(30));   // przelom miesiaca
        var (budget, error) = await Handler().HandleAsync(new CreateBudgetRequestDto("Październik", "PLN", 0m), default);

        Assert.Equal(CreateBudgetError.None, error);
        Assert.Equal(new DateOnly(2026, 10, 1), budget!.Month);
        Assert.Equal(2, await _db.Budgets.CountAsync());
    }

    // ── Bilans poczatkowy ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stores_the_opening_balance()
    {
        var (budget, _) = await Handler().HandleAsync(
            new CreateBudgetRequestDto("Domowy", "PLN", 2500.50m), default);

        Assert.Equal(2500.50m, budget!.InitialBalance);
        Assert.Equal(2500.50m, (await _db.Budgets.SingleAsync()).InitialBalance);
    }

    [Fact]
    public async Task Accepts_a_negative_opening_balance()
    {
        // Debet na koncie to legalny stan startowy — odrzucanie go wymuszaloby wpisanie
        // nieprawdy, zeby w ogole zaczac.
        var (budget, error) = await Handler().HandleAsync(
            new CreateBudgetRequestDto("Po remoncie", "PLN", -1200m), default);

        Assert.Equal(CreateBudgetError.None, error);
        Assert.Equal(-1200m, budget!.InitialBalance);
    }

    [Fact]
    public async Task Opening_balance_defaults_to_zero()
    {
        var (budget, _) = await Handler().HandleAsync(new CreateBudgetRequestDto("Zerowy", "PLN", 0m), default);

        Assert.Equal(0m, budget!.InitialBalance);
    }
}
