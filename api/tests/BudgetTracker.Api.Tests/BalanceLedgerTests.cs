using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Dashboard.Queries;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Bilans budżetu odczytywany z salda banku (<see cref="BalanceLedger"/>). Część czysta (bez bazy) i część na Postgresie:
/// dashboard, lista budżetów i podsumowanie importu muszą pokazywać TĘ SAMĄ liczbę. Dane zmyślone. Wymaga `docker compose up -d db`.
/// </summary>
public sealed class BalanceLedgerTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_bankbalance_test;Username=budget;Password=budget_dev_only";

    private static DateOnly D(int day) => new(2026, 9, day);

    private static LedgerInput Row(int id, int day, decimal amount, decimal? balance = null) => new(id, D(day), amount, balance);

    // ── Księga (bez bazy) ────────────────────────────────────────────────────────────────

    [Fact]
    public void Without_bank_balances_it_is_the_old_formula_initial_plus_amounts()
    {
        // Regresja: budżet bez salda z banku (same ręczne wpisy, stare importy) ma liczyć się dokładnie jak dotąd.
        var ledger = BalanceLedger.Build(1000m, [Row(1, 2, -100m), Row(2, 3, 50m), Row(3, 3, -20m)]);

        Assert.Equal(1000m, ledger.Opening);
        Assert.Equal(930m, ledger.Closing);
        Assert.Equal(900m, ledger.BalanceBefore(D(3)));
    }

    [Fact]
    public void The_ledger_says_whether_the_balance_comes_from_the_bank()
    {
        Assert.False(BalanceLedger.Build(1000m, [Row(1, 2, -100m)]).UsesBankBalances);
        Assert.True(BalanceLedger.Build(1000m, [Row(1, 2, -100m), Row(2, 3, -5m, 895m)]).UsesBankBalances);
    }

    [Fact]
    public void An_empty_budget_shows_its_initial_balance()
    {
        var ledger = BalanceLedger.Build(1234.56m, []);

        Assert.Equal(1234.56m, ledger.Closing);
        Assert.Equal(1234.56m, ledger.BalanceBefore(D(15)));
    }

    [Fact]
    public void With_bank_balances_the_balance_is_the_banks_and_the_initial_balance_is_not_used()
    {
        // Sedno zgłoszenia: bilans początkowy 1000 nie zgadza się ze stanem konta przed pierwszą transakcją (6100).
        // Wzór „początkowy + suma" dawał 1000 − 100 − 50 = 850, bank pokazuje 5950.
        var ledger = BalanceLedger.Build(1000m, [Row(1, 2, -100m, 6000m), Row(2, 3, -50m, 5950m)]);

        Assert.Equal(6100m, ledger.Opening);
        Assert.Equal(5950m, ledger.Closing);
    }

    [Fact]
    public void A_gap_between_two_files_does_not_shift_the_balance_after_it()
    {
        // Dwa pliki z różnych okresów: między 3. a 20. brakuje wpisów za 250. Saldo po ostatniej operacji to nadal saldo banku.
        var ledger = BalanceLedger.Build(0m, [Row(1, 3, -100m, 900m), Row(2, 20, -50m, 600m)]);

        Assert.Equal(600m, ledger.Closing);
        Assert.Equal(900m, ledger.BalanceBefore(D(20)));
    }

    [Fact]
    public void Rows_of_one_day_are_ordered_by_the_balance_chain_not_by_write_order()
    {
        // Ten sam dzień zapisany w odwrotnej kolejności niż w banku (Id rośnie, saldo nie): „ostatnia wg Id" miałaby saldo 1000.
        // Prawdziwa kolejność w banku: 1000 → 900 → 850 → 800, więc dzień zamyka 800.
        var ledger = BalanceLedger.Build(0m, [Row(1, 5, -50m, 800m), Row(2, 5, -100m, 900m), Row(3, 5, -50m, 850m), Row(4, 4, -10m, 1000m)]);

        Assert.Equal(800m, ledger.Closing);
        Assert.Equal([1000m, 900m, 850m, 800m], ledger.Entries.Select(e => e.Balance));
    }

    [Fact]
    public void The_previous_days_closing_balance_disambiguates_where_the_day_starts()
    {
        // Dwie operacje znoszą się (−10 i +10), więc oba porządki są spójne z samych sald. Rozstrzyga saldo zamykające poprzedni dzień.
        var ledger = BalanceLedger.Build(0m,
        [
            Row(1, 4, -5m, 100m),
            Row(2, 5, 10m, 100m),   // 90 -> 100
            Row(3, 5, -10m, 90m),   // 100 -> 90
        ]);

        // Poprzedni dzień zamyka 100, więc dzień 5 zaczyna od −10 (100 → 90), potem +10 (90 → 100).
        Assert.Equal(100m, ledger.Closing);
    }

    [Fact]
    public void Manual_rows_add_to_the_balance_and_the_next_bank_balance_takes_over_again()
    {
        var ledger = BalanceLedger.Build(0m,
        [
            Row(1, 2, -100m, 900m),
            Row(2, 3, -30m),          // ręczna: 900 -> 870
            Row(3, 4, -20m, 840m),    // bank mówi 840 (ręczna 30 była już w nim albo bank ma coś jeszcze) — bank wygrywa
        ]);

        Assert.Equal(870m, ledger.BalanceBefore(D(4)));
        Assert.Equal(840m, ledger.Closing);
    }

    [Fact]
    public void Inconsistent_balances_fall_back_to_write_order_without_failing()
    {
        // Saldo, które nie składa się w łańcuch (brakuje wiersza dnia): bez wyjątku, w kolejności zapisu.
        var ledger = BalanceLedger.Build(0m, [Row(1, 5, -10m, 500m), Row(2, 5, -10m, 300m)]);

        Assert.Equal(2, ledger.Entries.Count);
        Assert.Equal(300m, ledger.Closing);
    }

    [Fact]
    public void Balance_before_a_date_is_the_balance_after_the_last_earlier_transaction()
    {
        var ledger = BalanceLedger.Build(0m, [Row(1, 2, -100m, 900m), Row(2, 5, -50m, 850m)]);

        Assert.Equal(1000m, ledger.BalanceBefore(D(1)));   // przed pierwszą: saldo sprzed operacji
        Assert.Equal(1000m, ledger.BalanceBefore(D(2)));   // początek dnia 2 = przed jego operacjami
        Assert.Equal(900m, ledger.BalanceBefore(D(3)));
        Assert.Equal(850m, ledger.BalanceBefore(D(30)));
    }

    // ── Te same liczby na każdym ekranie (Postgres) ──────────────────────────────────────

    private AppDbContext _db = null!;
    private Guid _budgetId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        // Bilans początkowy 1000 NIE zgadza się z bankiem (przed pierwszą operacją bank miał 6100) — jak w zgłoszeniu.
        var budget = new Budget("Domowy", new DateOnly(2026, 1, 1), 1000m, DateTimeOffset.UtcNow);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
        _budgetId = budget.BusinessId;

        var now = DateTimeOffset.UtcNow;
        _db.Transactions.AddRange(
            // Dzień 5 zapisany w odwrotnej kolejności niż w banku: 6000 → 5900 → 5850.
            Tx(D(5), -50m, "B", 5850m, now),
            Tx(D(5), -100m, "A", 5900m, now),
            Tx(D(4), -100m, "PIERWSZA", 6000m, now));
        await _db.SaveChangesAsync();
    }

    private Transaction Tx(DateOnly date, decimal amount, string description, decimal? balance, DateTimeOffset now) =>
        new(date, amount, description, now, TransactionStatus.Confirmed, budgetBusinessId: _budgetId, balanceAfter: balance);

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Dashboard_shows_the_banks_balance_and_the_chart_follows_the_bank_within_the_day()
    {
        var localizer = new StringLocalizer<SharedResource>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));
        var handler = new GetDashboardQueryHandler(_db, localizer);

        var r = await handler.HandleAsync(D(1), D(30), _budgetId, default);

        Assert.Equal(5850m, r.Metrics.BudgetBalance);
        Assert.Equal(r.BudgetProgress[^1].Balance, r.Metrics.BudgetBalance);
        // Punkty dnia 5 idą wg banku (5900, potem 5850), nie wg kolejności zapisu (5850, potem 5900).
        Assert.Equal([6100m, 6000m, 5900m, 5850m, 5850m], r.BudgetProgress.Select(p => p.Balance));
    }

    [Fact]
    public async Task Budget_list_and_import_summary_show_the_same_balance_as_the_dashboard()
    {
        var row = (await new BudgetListItemReader(_db, TimeProvider.System).ReadAllAsync(default)).Single();

        Assert.Equal(5850m, row.Balance);
        Assert.Equal(1000m, row.InitialBalance);   // bilans początkowy zostaje w budżecie, tylko nie zaniża bilansu
        Assert.True(row.BalanceFromBank);          // ekran edycji wie, że bilans początkowy niczego tu nie zmienia

        var ledger = await BudgetLedgers.LoadAsync(_db, _budgetId, 1000m, default);
        Assert.Equal(row.Balance, ledger.Closing);
    }
}
