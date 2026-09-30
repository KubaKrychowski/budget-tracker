using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Commands;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Limits.Commands;
using BudgetTracker.Api.Features.Limits.Contracts;
using BudgetTracker.Api.Features.Limits.Queries;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Features.StandingOrders.Commands;
using BudgetTracker.Api.Features.StandingOrders.Consts;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Queries;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Okres rozliczeniowy (dzień wypłaty zamiast pierwszego dnia miesiąca): granice okresu, nazwa po miesiącu końcowym,
/// limity i zlecenia stałe liczone po okresie, walidacja dnia w edycji budżetu.
///
/// Dane są zmyślone. Część z bazą wymaga `docker compose up -d db`.
/// </summary>
public sealed class BillingPeriodTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_period_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś" to 29 września 2026 — wypłata z 28. już przyszła, więc trwa okres „październik" (28.09–27.10).</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateOnly October = new(2026, 10, 1);

    private AppDbContext _db = null!;
    private Budget _budget = null!;
    private Category _food = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _food = new Category("Jedzenie");
        _budget = new Budget("Domowy", new DateOnly(2026, 1, 1), 0m, _clock.GetUtcNow());
        _db.AddRange(_food, _budget);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Logika okresu (bez bazy) ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-09-27", 28, "2026-09-01")] // dzień przed wypłatą — jeszcze okres „wrzesień"
    [InlineData("2026-09-28", 28, "2026-10-01")] // dzień wypłaty otwiera okres „październik"
    [InlineData("2026-10-27", 28, "2026-10-01")] // ostatni dzień okresu „październik"
    [InlineData("2026-10-28", 28, "2026-11-01")]
    [InlineData("2026-12-28", 28, "2027-01-01")] // przełom roku: okres „styczeń 2027"
    [InlineData("2026-12-31", 28, "2027-01-01")]
    [InlineData("2027-01-15", 28, "2027-01-01")]
    [InlineData("2026-09-15", 1, "2026-09-01")] // dzień 1 = zwykły miesiąc kalendarzowy
    [InlineData("2026-09-30", 1, "2026-09-01")]
    public void Okres_nosi_nazwe_miesiaca_w_ktorym_sie_konczy(string date, int startDay, string expectedKey)
    {
        Assert.Equal(DateOnly.Parse(expectedKey), BillingPeriod.KeyOf(DateOnly.Parse(date), startDay));
    }

    [Fact]
    public void Granice_okresu_28_to_28_dnia_do_27_nastepnego()
    {
        Assert.Equal(new DateOnly(2026, 9, 28), BillingPeriod.From(October, 28));
        Assert.Equal(new DateOnly(2026, 10, 28), BillingPeriod.ToExclusive(October, 28));
        Assert.Equal(new DateOnly(2026, 10, 27), BillingPeriod.To(October, 28));
    }

    [Fact]
    public void Granice_okresu_styczen_siegaja_do_grudnia_poprzedniego_roku()
    {
        var january = new DateOnly(2027, 1, 1);

        Assert.Equal(new DateOnly(2026, 12, 28), BillingPeriod.From(january, 28));
        Assert.Equal(new DateOnly(2027, 1, 27), BillingPeriod.To(january, 28));
    }

    [Fact]
    public void Dzien_1_daje_dokladnie_miesiac_kalendarzowy()
    {
        // Ochrona istniejących budżetów: domyślny dzień 1 nie może przesunąć żadnej granicy.
        Assert.Equal(September, BillingPeriod.From(September, 1));
        Assert.Equal(October, BillingPeriod.ToExclusive(September, 1));
        Assert.Equal(new DateOnly(2026, 9, 30), BillingPeriod.To(September, 1));
    }

    [Fact]
    public void Kazdy_dozwolony_dzien_daje_okres_bez_luk_i_nakladek()
    {
        // Okresy muszą się stykać: koniec jednego to początek następnego, w każdym miesiącu (także lutym).
        for (var startDay = BillingPeriod.MinStartDay; startDay <= BillingPeriod.MaxStartDay; startDay++)
        {
            for (var key = new DateOnly(2027, 1, 1); key < new DateOnly(2029, 1, 1); key = key.AddMonths(1))
            {
                Assert.Equal(BillingPeriod.ToExclusive(key, startDay), BillingPeriod.From(key.AddMonths(1), startDay));
                Assert.Equal(key, BillingPeriod.KeyOf(BillingPeriod.From(key, startDay), startDay));
                Assert.Equal(key, BillingPeriod.KeyOf(BillingPeriod.To(key, startDay), startDay));
            }
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(28, true)]
    [InlineData(29, false)]
    [InlineData(31, false)]
    public void Dozwolone_sa_dni_od_1_do_28(int day, bool valid) =>
        Assert.Equal(valid, BillingPeriod.IsValidStartDay(day));

    // ── Edycja budżetu ───────────────────────────────────────────────────────────────────

    private UpdateBudgetCommandHandler UpdateBudget() => new(_db, new BudgetLookup(_db), new BudgetListItemReader(_db, _clock));

    [Fact]
    public async Task Edycja_ustawia_dzien_okresu_a_brak_wartosci_zostawia_go_bez_zmian()
    {
        var set = await UpdateBudget().HandleAsync(
            _budget.BusinessId, new UpdateBudgetRequestDto("Domowy", 0m, 28), default);
        Assert.Equal(28, set.PeriodStartDay);

        // Klient, który o okresie nie wie (CLI, stary front), nie może go po cichu zresetować do 1.
        var untouched = await UpdateBudget().HandleAsync(
            _budget.BusinessId, new UpdateBudgetRequestDto("Nowa nazwa", 0m), default);
        Assert.Equal(28, untouched.PeriodStartDay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(-3)]
    public async Task Dzien_okresu_poza_zakresem_to_blad(int day)
    {
        await Assert.ThrowsAsync<BudgetPeriodStartDayInvalidException>(() =>
            UpdateBudget().HandleAsync(_budget.BusinessId, new UpdateBudgetRequestDto("Domowy", 0m, day), default));
    }

    [Fact]
    public async Task Nowy_budzet_ma_miesiac_kalendarzowy()
    {
        Assert.Equal(1, (await _db.Budgets.SingleAsync()).PeriodStartDay);
    }

    [Fact]
    public async Task Baza_odrzuca_dzien_okresu_poza_zakresem_takze_poza_API()
    {
        await _db.Database.ExecuteSqlRawAsync("UPDATE \"Budgets\" SET \"PeriodStartDay\" = 28");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _db.Database.ExecuteSqlRawAsync("UPDATE \"Budgets\" SET \"PeriodStartDay\" = 29"));
    }

    // ── Limity ───────────────────────────────────────────────────────────────────────────

    private async Task UsePeriodStartDayAsync(int day)
    {
        _budget.ChangePeriodStartDay(day);
        await _db.SaveChangesAsync();
    }

    private LimitsBudgetScope LimitsScope() => new(_db, _clock);

    private GetLimitsQueryHandler LimitsQuery() => new(_db, LimitsScope(), new LimitCategories(_db), new LimitSpending(_db));

    private SetLimitCommandHandler SetLimit() =>
        new(_db, LimitsScope(), new LimitCategories(_db), new FakeCurrentUserAccessor(Guid.NewGuid()));

    private Task<LimitSavedResponseDto> SetLimitAsync(decimal amount, DateOnly from) =>
        SetLimit().HandleAsync(new SetLimitRequestDto(_budget.BusinessId, _food.BusinessId, amount, 80, from), default);

    private void Spend(DateOnly date, decimal amount) =>
        _db.Transactions.Add(new Transaction(
            date, -amount, "TEST", default, TransactionStatus.Confirmed,
            categoryId: _food.Id, budgetBusinessId: _budget.BusinessId));

    [Fact]
    public async Task Limity_licza_wydatki_z_okresu_a_nie_z_miesiaca_kalendarzowego()
    {
        // Sedno zadania: 30.09 i 05.10 należą do okresu „październik" (28.09–27.10), 20.09 do „września".
        await UsePeriodStartDayAsync(28);
        await SetLimitAsync(1000m, September);
        Spend(new DateOnly(2026, 9, 20), 100m);
        Spend(new DateOnly(2026, 9, 30), 200m);
        Spend(new DateOnly(2026, 10, 5), 300m);
        Spend(new DateOnly(2026, 10, 28), 400m); // już okres „listopad"
        await _db.SaveChangesAsync();

        var october = await LimitsQuery().HandleAsync(_budget.BusinessId, null, default);

        Assert.Equal(October, october.Month);
        Assert.Equal(new DateOnly(2026, 9, 28), october.PeriodFrom);
        Assert.Equal(new DateOnly(2026, 10, 27), october.PeriodTo);
        Assert.Equal(500m, october.Limits.Single().Spent);

        var september = await LimitsQuery().HandleAsync(_budget.BusinessId, September, default);
        Assert.Equal(100m, september.Limits.Single().Spent);
        Assert.True(september.ReadOnly);
    }

    [Fact]
    public async Task Dzien_1_zachowuje_zachowanie_sprzed_okresow()
    {
        // Regresja: budżet bez ustawionego okresu dalej liczy po miesiącu kalendarzowym.
        await SetLimitAsync(1000m, September);
        Spend(new DateOnly(2026, 9, 5), 100m);
        Spend(new DateOnly(2026, 9, 30), 200m);
        Spend(new DateOnly(2026, 10, 2), 900m);
        await _db.SaveChangesAsync();

        var response = await LimitsQuery().HandleAsync(_budget.BusinessId, null, default);

        Assert.Equal(September, response.Month);
        Assert.Equal(new DateOnly(2026, 9, 1), response.PeriodFrom);
        Assert.Equal(new DateOnly(2026, 9, 30), response.PeriodTo);
        Assert.Equal(300m, response.Limits.Single().Spent);
    }

    [Fact]
    public async Task Dzien_przed_wyplata_biezacy_okres_to_jeszcze_ten_z_nazwa_biezacego_miesiaca()
    {
        await UsePeriodStartDayAsync(28);
        var dayBeforePayday = new FakeTimeProvider(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero));
        var query = new GetLimitsQueryHandler(_db, new LimitsBudgetScope(_db, dayBeforePayday), new LimitCategories(_db), new LimitSpending(_db));

        var response = await query.HandleAsync(_budget.BusinessId, null, default);

        Assert.Equal(September, response.Month);
        Assert.Equal(September, response.CurrentMonth);
    }

    [Fact]
    public async Task Limit_bez_okresu_ustawiony_na_biezacy_okres_nie_jest_zamknieta_historia()
    {
        // 29.09 przy dniu 28 „bieżący" to październik — limit wolno zmienić od października, nie od września.
        await UsePeriodStartDayAsync(28);
        await SetLimitAsync(1000m, September);

        await SetLimitAsync(1500m, October);

        var october = await LimitsQuery().HandleAsync(_budget.BusinessId, October, default);
        Assert.Equal(1500m, october.Limits.Single().Limit);
        var september = await LimitsQuery().HandleAsync(_budget.BusinessId, September, default);
        Assert.Equal(1000m, september.Limits.Single().Limit);
    }

    [Fact]
    public async Task Miesieczny_limit_na_liscie_budzetow_liczy_sie_z_biezacego_okresu_budzetu()
    {
        await UsePeriodStartDayAsync(28);
        await SetLimitAsync(1000m, September);
        await SetLimitAsync(1500m, October);

        var row = (await new BudgetListItemReader(_db, _clock).ReadAllAsync(default)).Single();

        // Kalendarzowo jest wrzesień (1000), ale okres budżetu to już październik (1500).
        Assert.Equal(1500m, row.MonthlyLimit);
    }

    // ── Zlecenia stałe ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Zlecenie_stale_zeszlo_w_okresie_gdy_data_platnosci_wpada_w_jego_granice()
    {
        await UsePeriodStartDayAsync(28);
        var scope = new StandingOrdersBudgetScope(_db, _clock);
        var matcher = new StandingOrderMatcher(_db);
        await new CreateStandingOrderCommandHandler(_db, scope, matcher, new FakeCurrentUserAccessor(Guid.NewGuid()))
            .HandleAsync(new SaveStandingOrderRequestDto(
                null, "Czynsz", 2200m, StandingOrderRhythm.Monthly, null,
                [new StandingOrderRuleRequestDto("czynsz", 2000m, 2500m)]), default);

        // Czynsz zapłacony 29.09 to koszt okresu „październik", nie „września".
        var paid = new Transaction(new DateOnly(2026, 9, 29), -2200m, "CZYNSZ", default, TransactionStatus.Confirmed,
            categoryId: _food.Id, budgetBusinessId: _budget.BusinessId);
        _db.Transactions.Add(paid);
        await _db.SaveChangesAsync();
        await matcher.PinAsync(_budget.BusinessId, [paid.BusinessId], default);

        var query = new GetStandingOrdersQueryHandler(_db, scope);
        var october = await query.HandleAsync(_budget.BusinessId, null, default);
        var september = await query.HandleAsync(_budget.BusinessId, September, default);

        Assert.Equal(StandingOrderMonthState.Paid, october.Orders.Single().State);
        Assert.Equal(new DateOnly(2026, 9, 28), october.PeriodFrom);
        Assert.Equal(StandingOrderMonthState.Missed, september.Orders.Single().State);
    }
}
