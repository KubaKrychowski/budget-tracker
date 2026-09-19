using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.StandingOrders.Commands;
using BudgetTracker.Api.Features.StandingOrders.Consts;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Exceptions;
using BudgetTracker.Api.Features.StandingOrders.Queries;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Queries;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Zlecenia stałe: reguła przypina transakcje wstecz i z importu, bez zmiany kategorii; ręczne odpięcie przeżywa
/// zmianę reguły; stan w miesiącu; zlecenia idą razem z cyklem życia budżetu.
///
/// Opisy i kwoty są zmyślone. Wymaga `docker compose up -d db`.
/// </summary>
public sealed class StandingOrdersTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_standingorders_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś" to 14 września 2026 — sierpień zamknięty, wrzesień trwa.</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly August = new(2026, 8, 1);
    private static readonly DateOnly September = new(2026, 9, 1);

    private AppDbContext _db = null!;
    private Budget _budget = null!;
    private Category _flat = null!;
    private Category _other = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _flat = new Category("Mieszkanie");
        _other = new Category("Inne");
        _budget = new Budget("Domowy", new DateOnly(2026, 1, 1), 0m, _clock.GetUtcNow());
        _db.AddRange(_flat, _other, _budget);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private StandingOrdersBudgetScope Scope() => new(_db, _clock);

    private StandingOrderMatcher Matcher() => new(_db);

    private CreateStandingOrderCommandHandler Create() =>
        new(_db, Scope(), Matcher(), new FakeCurrentUserAccessor(Guid.NewGuid()));

    private UpdateStandingOrderCommandHandler Update() => new(_db, Matcher());

    private GetStandingOrdersQueryHandler Query() => new(_db, Scope());

    private static SaveStandingOrderRequestDto Rent(
        decimal from = 2000m, decimal to = 2500m, string pattern = "czynsz",
        StandingOrderRhythm rhythm = StandingOrderRhythm.Monthly, int? dueMonth = null, string name = "Czynsz") =>
        new(null, name, 2200m, rhythm, dueMonth, [new StandingOrderRuleRequestDto(pattern, from, to)]);

    private static StandingOrderRuleRequestDto Rule(string pattern, decimal from, decimal to) => new(pattern, from, to);

    private PreviewStandingOrderQueryHandler Preview() => new(_db, Scope(), Matcher());

    private EndStandingOrderCommandHandler End() => new(_db);

    private Transaction Add(DateOnly date, decimal amount, string description, Category? category = null, Guid? budget = null)
    {
        var t = new Transaction(date, amount, description, _clock.GetUtcNow(), TransactionStatus.Confirmed,
            categoryId: (category ?? _flat).Id, budgetBusinessId: budget ?? _budget.BusinessId);
        _db.Transactions.Add(t);
        return t;
    }

    private async Task<Guid?> PinOf(Transaction t)
    {
        _db.ChangeTracker.Clear();
        return await _db.Transactions.Where(x => x.Id == t.Id).Select(x => x.StandingOrderBusinessId).SingleAsync();
    }

    private async Task<StandingOrderRowResponseDto> RowAsync(DateOnly? month = null) =>
        Assert.Single((await Query().HandleAsync(_budget.BusinessId, month, default)).Orders);

    // ── Dopasowanie ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Zapis_zlecenia_przypina_WSTECZ_tylko_pasujace_wydatki_i_nie_zmienia_kategorii()
    {
        var rent = Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ ZA SIERPIEN", _other);
        var income = Add(new DateOnly(2026, 8, 6), 2200m, "ZWROT CZYNSZ");
        var tooBig = Add(new DateOnly(2026, 8, 7), -3000m, "czynsz + media");
        var otherTitle = Add(new DateOnly(2026, 8, 8), -2200m, "RATA KREDYTU");
        var otherBudget = new Budget("Wariant", new DateOnly(2026, 1, 1), 0m, _clock.GetUtcNow());
        _db.Budgets.Add(otherBudget);
        await _db.SaveChangesAsync();
        var foreign = Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ", budget: otherBudget.BusinessId);
        await _db.SaveChangesAsync();

        var saved = await Create().HandleAsync(Rent() with { BudgetId = _budget.BusinessId }, default);

        Assert.Equal(1, saved.LinkedCount);
        Assert.Equal(saved.Id, await PinOf(rent));
        Assert.Null(await PinOf(income));
        Assert.Null(await PinOf(tooBig));
        Assert.Null(await PinOf(otherTitle));
        Assert.Null(await PinOf(foreign));

        // Zlecenie TYLKO się przypina — czynsz w „Inne" dalej jest w „Inne".
        Assert.Equal(_other.Id, await _db.Transactions.Where(x => x.Id == rent.Id).Select(x => x.CategoryId).SingleAsync());
    }

    [Fact]
    public async Task Fraza_to_dane_a_nie_wzorzec_LIKE()
    {
        // Bez ucieczki „15%" zamienia się we wzorzec „zawiera 15” i łapie „150 PLN".
        var match = Add(new DateOnly(2026, 8, 5), -100m, "OPLATA 15% PROWIZJI");
        var noMatch = Add(new DateOnly(2026, 8, 6), -100m, "OPLATA 150 PLN");
        await _db.SaveChangesAsync();

        await Create().HandleAsync(Rent(50m, 150m, "15%") with { ExpectedAmount = 100m }, default);

        Assert.NotNull(await PinOf(match));
        Assert.Null(await PinOf(noMatch));
    }

    [Fact]
    public async Task Reczne_odpiecie_przezywa_zmiane_reguly()
    {
        // ⚠️ Bez pamięci odpięcia każda zmiana reguły przypinałaby transakcję z powrotem.
        var mistake = Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ GARAZ");
        var rent = Add(new DateOnly(2026, 8, 6), -2200m, "CZYNSZ MIESZKANIE");
        await _db.SaveChangesAsync();
        var saved = await Create().HandleAsync(Rent(), default);

        // Każde żądanie ma w aplikacji świeży kontekst — przypięcie zrobione ExecuteUpdate nie siedzi w trackerze.
        _db.ChangeTracker.Clear();
        await new UnpinTransactionCommandHandler(_db).HandleAsync(mistake.BusinessId, default);
        _db.ChangeTracker.Clear();
        await Update().HandleAsync(saved.Id, Rent(1900m, 2600m), default);

        Assert.Null(await PinOf(mistake));
        Assert.Equal(saved.Id, await PinOf(rent));
    }

    [Fact]
    public async Task Transakcja_nalezy_do_jednego_zlecenia_a_podglad_mowi_ile_jest_zajetych()
    {
        var t = Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ I GARAZ");
        await _db.SaveChangesAsync();
        var first = await Create().HandleAsync(Rent(), default);

        var preview = await Preview().HandleAsync(
            new StandingOrderPreviewRequestDto(null, null, [Rule("garaz", 100m, 3000m)]), default);
        var second = await Create().HandleAsync(Rent(100m, 3000m, "garaz", name: "Garaż"), default);

        Assert.Equal(1, preview.MatchCount);
        Assert.Equal(1, preview.TakenByOtherOrders);
        Assert.Equal(0, second.LinkedCount);
        Assert.Equal(first.Id, await PinOf(t));
    }

    [Fact]
    public async Task Wiele_regul_laczy_LUB_a_kazda_ma_wlasny_zakres_kwot()
    {
        var byRent = Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ SIERPIEN");
        var byLease = Add(new DateOnly(2026, 7, 5), -2100m, "NAJEM LOKALU LIPIEC");
        var bothRules = Add(new DateOnly(2026, 6, 5), -2100m, "CZYNSZ NAJEM LOKALU");
        // „najem lokalu" za 900 zł nie mieści się w zakresie SWOJEJ reguły — zakres innej reguły go nie ratuje.
        var wrongRange = Add(new DateOnly(2026, 6, 20), -900m, "NAJEM LOKALU GARAZ");
        await _db.SaveChangesAsync();

        var request = Rent() with { Rules = [Rule("czynsz", 2000m, 2500m), Rule("najem lokalu", 2000m, 2200m)] };
        var preview = await Preview().HandleAsync(new StandingOrderPreviewRequestDto(null, null, request.Rules), default);
        var saved = await Create().HandleAsync(request, default);

        Assert.Equal(3, preview.MatchCount);
        Assert.Equal(3, saved.LinkedCount);
        Assert.Equal(saved.Id, await PinOf(byRent));
        Assert.Equal(saved.Id, await PinOf(byLease));
        Assert.Equal(saved.Id, await PinOf(bothRules));
        Assert.Null(await PinOf(wrongRange));

        // Reguły wracają z jsonb w tej samej kolejności, co w modalu.
        var row = await RowAsync(August);
        Assert.Equal(["czynsz", "najem lokalu"], row.Rules.Select(r => r.TitlePattern));
        Assert.Equal(2200m, row.Rules[1].AmountTo);
    }

    [Fact]
    public async Task Import_przypina_nowe_transakcje_do_zlecen_budzetu()
    {
        await Create().HandleAsync(Rent(), default);
        var imported = Add(new DateOnly(2026, 9, 5), -2200m, "CZYNSZ WRZESIEN");
        await _db.SaveChangesAsync();

        await Matcher().PinAsync(_budget.BusinessId, [imported.BusinessId], default);

        Assert.NotNull(await PinOf(imported));
    }

    [Fact]
    public async Task Usuniecie_zlecenia_zdejmuje_przypiecia_i_pamiec_odpiec()
    {
        var a = Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ A");
        var b = Add(new DateOnly(2026, 7, 5), -2200m, "CZYNSZ B");
        await _db.SaveChangesAsync();
        var saved = await Create().HandleAsync(Rent(), default);
        _db.ChangeTracker.Clear();
        await new UnpinTransactionCommandHandler(_db).HandleAsync(b.BusinessId, default);
        _db.ChangeTracker.Clear();

        await new DeleteStandingOrderCommandHandler(_db, Matcher()).HandleAsync(saved.Id, default);

        _db.ChangeTracker.Clear();
        var rows = await _db.Transactions.Select(x => new { x.StandingOrderBusinessId, x.StandingOrderUnpinnedFrom }).ToListAsync();
        Assert.All(rows, r => { Assert.Null(r.StandingOrderBusinessId); Assert.Null(r.StandingOrderUnpinnedFrom); });
        Assert.Empty(await _db.StandingOrders.ToListAsync());
    }

    // ── Stan w miesiącu ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Zeszlo_w_innej_kwocie_to_osobny_stan_a_mediana_dnia_daje_zwykle()
    {
        Add(new DateOnly(2026, 6, 5), -2200m, "CZYNSZ");
        Add(new DateOnly(2026, 7, 6), -2200m, "CZYNSZ");
        Add(new DateOnly(2026, 8, 20), -2350m, "CZYNSZ");
        await _db.SaveChangesAsync();
        await Create().HandleAsync(Rent(), default);

        var july = await RowAsync(new DateOnly(2026, 7, 1));
        var august = await RowAsync(August);

        Assert.Equal(StandingOrderMonthState.Paid, july.State);
        Assert.Equal(StandingOrderMonthState.PaidDifferentAmount, august.State);
        Assert.Equal(2350m, august.PaidAmount);
        Assert.Equal(6, august.UsualDay);
        Assert.Equal(3, august.LinkedCount);
        Assert.Equal("Mieszkanie", august.CategoryName);
    }

    [Fact]
    public async Task Brak_przypiecia_czeka_w_biezacym_miesiacu_a_w_zamknietym_nie_zeszlo()
    {
        await Create().HandleAsync(Rent(), default);

        Assert.Equal(StandingOrderMonthState.Waiting, (await RowAsync()).State);
        Assert.Equal(StandingOrderMonthState.Missed, (await RowAsync(August)).State);
    }

    [Fact]
    public async Task Zlecenie_roczne_poza_swoim_miesiacem_nie_przypada_a_suma_liczy_je_po_dwunastu()
    {
        await Create().HandleAsync(Rent() with { ExpectedAmount = 1200m, Rhythm = StandingOrderRhythm.Yearly, DueMonth = 5 }, default);

        var response = await Query().HandleAsync(_budget.BusinessId, September, default);

        Assert.Equal(StandingOrderMonthState.NotDue, Assert.Single(response.Orders).State);
        Assert.Equal(0, response.DueCount);
        Assert.Equal(100m, response.MonthlyTotal);
    }

    [Fact]
    public async Task Kwartalne_przypada_co_trzy_miesiace_od_wskazanego()
    {
        var order = new StandingOrder(_budget.BusinessId, "Woda", 150m, StandingOrderRhythm.Quarterly, 2, _clock.GetUtcNow());

        Assert.True(order.IsDueIn(new DateOnly(2026, 2, 1)));
        Assert.True(order.IsDueIn(new DateOnly(2026, 11, 1)));
        Assert.False(order.IsDueIn(new DateOnly(2026, 9, 1)));
    }

    // ── Zakończenie ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Zakonczone_zlecenie_do_ostatniego_miesiaca_wyglada_normalnie_a_potem_jest_zakonczone_i_poza_suma()
    {
        Add(new DateOnly(2026, 7, 5), -2200m, "CZYNSZ");
        await _db.SaveChangesAsync();
        var saved = await Create().HandleAsync(Rent(), default);
        await Create().HandleAsync(Rent(pattern: "internet", from: 50m, to: 70m, name: "Internet") with { ExpectedAmount = 60m }, default);
        _db.ChangeTracker.Clear();

        await End().EndAsync(saved.Id, new EndStandingOrderRequestDto(new DateOnly(2026, 7, 31)), default);

        var july = await Query().HandleAsync(_budget.BusinessId, new DateOnly(2026, 7, 1), default);
        var september = await Query().HandleAsync(_budget.BusinessId, September, default);
        var rentInJuly = july.Orders.Single(o => o.Name == "Czynsz");
        var rentInSeptember = september.Orders.Single(o => o.Name == "Czynsz");

        Assert.Equal(StandingOrderMonthState.Paid, rentInJuly.State);
        Assert.Equal(2260m, july.MonthlyTotal);
        Assert.Equal(StandingOrderMonthState.Ended, rentInSeptember.State);
        Assert.Equal(new DateOnly(2026, 7, 1), rentInSeptember.EndMonth);
        Assert.Equal(60m, september.MonthlyTotal);
        Assert.Equal(1, september.DueCount);
        Assert.Equal(60m, september.WaitingAmount);
    }

    [Fact]
    public async Task Zakonczenie_zostawia_przypiecia_a_import_po_ostatnim_miesiacu_juz_nie_przypina()
    {
        var old = Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ SIERPIEN");
        await _db.SaveChangesAsync();
        var saved = await Create().HandleAsync(Rent(), default);
        _db.ChangeTracker.Clear();
        await End().EndAsync(saved.Id, new EndStandingOrderRequestDto(new DateOnly(2026, 7, 1)), default);

        var late = Add(new DateOnly(2026, 9, 5), -2200m, "CZYNSZ WRZESIEN");
        var lastMonth = Add(new DateOnly(2026, 7, 30), -2200m, "CZYNSZ LIPIEC");
        await _db.SaveChangesAsync();
        await Matcher().PinAsync(_budget.BusinessId, [late.BusinessId, lastMonth.BusinessId], default);

        // Sierpniowy czynsz był przypięty przed zakończeniem — dialog obiecuje, że zostaje.
        Assert.Equal(saved.Id, await PinOf(old));
        Assert.Equal(saved.Id, await PinOf(lastMonth));
        Assert.Null(await PinOf(late));
    }

    [Fact]
    public async Task Wznowienie_przywraca_zlecenie_i_przypinanie()
    {
        var saved = await Create().HandleAsync(Rent(), default);
        _db.ChangeTracker.Clear();
        await End().EndAsync(saved.Id, new EndStandingOrderRequestDto(August), default);
        _db.ChangeTracker.Clear();

        await End().ResumeAsync(saved.Id, default);

        var imported = Add(new DateOnly(2026, 9, 5), -2200m, "CZYNSZ WRZESIEN");
        await _db.SaveChangesAsync();
        await Matcher().PinAsync(_budget.BusinessId, [imported.BusinessId], default);
        var row = await RowAsync(September);
        Assert.Null(row.EndMonth);
        Assert.Equal(StandingOrderMonthState.Paid, row.State);
    }

    [Fact]
    public async Task Zakonczenie_nieznanego_zlecenia_to_404()
    {
        await Assert.ThrowsAsync<StandingOrderNotFoundException>(
            () => End().EndAsync(Guid.NewGuid(), new EndStandingOrderRequestDto(August), default));
    }

    // ── Cykl życia budżetu ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Usuniecie_budzetu_zabiera_zlecenia_przywrocenie_je_oddaje_a_purge_kasuje()
    {
        await Create().HandleAsync(Rent(), default);
        var children = new BudgetChildren(_db);
        var at = _clock.GetUtcNow();

        await children.SoftDeleteSavingsAsync(_budget, at, default);
        Assert.Empty(await _db.StandingOrders.ToListAsync());

        await children.RestoreAsync(_budget, at, default);
        Assert.Single(await _db.StandingOrders.ToListAsync());

        await children.SoftDeleteSavingsAsync(_budget, at, default);
        _budget.MarkDeleted(at);
        await _db.SaveChangesAsync();
        await new BudgetPurger(_db).PurgeAsync(null, default);
        Assert.Empty(await _db.StandingOrders.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Reset_budzetu_zostawia_zlecenie()
    {
        // Zlecenie jest ZASADĄ budżetu, jak cel oszczędnościowy — reset czyści dane, nie postanowienia.
        await Create().HandleAsync(Rent(), default);

        await new BudgetChildren(_db).SoftDeleteAsync(_budget, _clock.GetUtcNow(), default);

        Assert.Single(await _db.StandingOrders.ToListAsync());
    }

    // ── Lista transakcji ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lista_transakcji_filtruje_po_zleceniu_i_podaje_jego_nazwe()
    {
        Add(new DateOnly(2026, 8, 5), -2200m, "CZYNSZ");
        Add(new DateOnly(2026, 8, 6), -50m, "KAWA");
        await _db.SaveChangesAsync();
        var saved = await Create().HandleAsync(Rent(), default);

        var handler = new GetTransactionsListQueryHandler(
            new TransactionBudgetScope(_db, _clock), new TransactionFilters(_db), new TransactionListItemReader(_db));
        var filter = new TransactionFilterRequestDto(
            [_budget.BusinessId], null, null, null, false, TransactionDirection.All, null, null, null, null, saved.Id);

        var response = await handler.HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal("CZYNSZ", Assert.Single(response.Items).Description);
        Assert.Equal("Czynsz", response.StandingOrderName);
    }

    // ── Walidacja ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Walidacja_odrzuca_krotka_fraze_odwrocony_zakres_i_roczne_bez_miesiaca()
    {
        await Assert.ThrowsAsync<StandingOrderPatternInvalidException>(() => Create().HandleAsync(Rent(pattern: "cz"), default));
        await Assert.ThrowsAsync<StandingOrderAmountInvalidException>(() => Create().HandleAsync(Rent(2500m, 2000m), default));
        await Assert.ThrowsAsync<StandingOrderDueMonthInvalidException>(
            () => Create().HandleAsync(Rent(rhythm: StandingOrderRhythm.Yearly), default));
        await Assert.ThrowsAsync<StandingOrderNameRequiredException>(() => Create().HandleAsync(Rent(name: "  "), default));
        await Assert.ThrowsAsync<StandingOrderRulesInvalidException>(() => Create().HandleAsync(Rent() with { Rules = [] }, default));
        await Assert.ThrowsAsync<StandingOrderPatternInvalidException>(
            () => Create().HandleAsync(Rent() with { Rules = [Rule("czynsz", 1m, 2m), Rule("x", 1m, 2m)] }, default));
    }

    [Fact]
    public async Task Stan_zlecenia_jedzie_do_frontu_jako_NAZWA()
    {
        Assert.Equal("\"PaidDifferentAmount\"", System.Text.Json.JsonSerializer.Serialize(StandingOrderMonthState.PaidDifferentAmount));
        Assert.Equal("\"Yearly\"", System.Text.Json.JsonSerializer.Serialize(StandingOrderRhythm.Yearly));
        await Task.CompletedTask;
    }
}
