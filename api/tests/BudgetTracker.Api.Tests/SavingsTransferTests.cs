using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Commands;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Dashboard.Queries;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Queries;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Powiązany budżet oszczędnościowy (#10): reguła transferu na budżecie głównym przypina własne
/// transakcje niezależnie od znaku kwoty, wyklucza je z sum wydatków/przychodów (nie z bilansu),
/// przeżywa ręczne odpięcie, a stan konta oszczędnościowego liczy się z bilansu powiązanego budżetu
/// zamiast zgadywać po kategorii. Opisy i kwoty są zmyślone. Wymaga `docker compose up -d db`.
/// </summary>
public sealed class SavingsTransferTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_savingstransfer_test;Username=budget;Password=budget_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));
    private AppDbContext _db = null!;
    private Budget _main = null!;
    private Budget _savings = null!;
    private Category _groceries = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _groceries = new Category("Jedzenie");
        _main = new Budget("Domowy", new DateOnly(2026, 9, 1), 1000m, _clock.GetUtcNow());
        _savings = new Budget("Domowy — Oszczędności", new DateOnly(2026, 9, 1), 0m, _clock.GetUtcNow());
        _db.AddRange(_groceries, _main, _savings);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private SavingsTransferMatcher Matcher() => new(_db);
    private BudgetLookup Lookup() => new(_db);
    private UpdateSavingsLinkCommandHandler UpdateLink() => new(_db, Lookup(), Matcher());
    private UnpinSavingsTransferCommandHandler Unpin() => new(_db);
    private DeleteBudgetCommandHandler DeleteBudget() => new(_db, Lookup(), new BudgetChildren(_db), Matcher(), _clock);

    private static UpdateSavingsLinkRequestDto LinkRequest(
        Guid target, string pattern = "przelew wlasny", decimal from = 0m, decimal to = 5000m) =>
        new(target, [new TitleAmountRuleRequestDto(pattern, from, to)]);

    private Transaction Add(DateOnly date, decimal amount, string description, Budget? budget = null, Category? category = null)
    {
        var t = new Transaction(date, amount, description, _clock.GetUtcNow(), TransactionStatus.Confirmed,
            categoryId: category?.Id, budgetBusinessId: (budget ?? _main).BusinessId);
        _db.Transactions.Add(t);
        return t;
    }

    private async Task<Guid?> PinOf(Transaction t)
    {
        _db.ChangeTracker.Clear();
        return await _db.Transactions.Where(x => x.Id == t.Id).Select(x => x.SavingsTransferBudgetBusinessId).SingleAsync();
    }

    // ── Dopasowanie ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Regula_lapie_oba_kierunki_transferu_niezaleznie_od_znaku_kwoty()
    {
        var toSavings = Add(new DateOnly(2026, 9, 2), -500m, "PRZELEW WLASNY NA OSZCZEDNOSCI");
        var fromSavings = Add(new DateOnly(2026, 9, 3), 200m, "PRZELEW WLASNY Z OSZCZEDNOSCI");
        var groceries = Add(new DateOnly(2026, 9, 4), -80m, "BIEDRONKA", category: _groceries);
        await _db.SaveChangesAsync();

        var saved = await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);

        Assert.Equal(2, saved.LinkedCount);
        Assert.Equal(_savings.BusinessId, await PinOf(toSavings));
        Assert.Equal(_savings.BusinessId, await PinOf(fromSavings));
        Assert.Null(await PinOf(groceries));

        // Przypięcie NIE zmienia kategorii — historia zostaje 1:1 z bankiem.
        Assert.Null(await _db.Transactions.Where(x => x.Id == toSavings.Id).Select(x => x.CategoryId).SingleAsync());
    }

    [Fact]
    public async Task Reczne_odpiecie_przezywa_zmiane_regul()
    {
        var mistake = Add(new DateOnly(2026, 9, 2), -50m, "PRZELEW WLASNY POMYLKA");
        var real = Add(new DateOnly(2026, 9, 3), -500m, "PRZELEW WLASNY PRAWDZIWY");
        await _db.SaveChangesAsync();
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);

        _db.ChangeTracker.Clear();
        await Unpin().HandleAsync(mistake.BusinessId, default);
        _db.ChangeTracker.Clear();

        // Reguła się zmienia (szerszy zakres), ale odpięcie ma przeżyć ponowne dopasowanie.
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId, from: 0m, to: 10000m), default);

        Assert.Null(await PinOf(mistake));
        Assert.Equal(_savings.BusinessId, await PinOf(real));
    }

    [Fact]
    public async Task Import_przypina_nowe_transakcje_wedlug_regul_budzetu()
    {
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);
        var imported = Add(new DateOnly(2026, 9, 10), -500m, "PRZELEW WLASNY WRZESIEN");
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var budget = await _db.Budgets.SingleAsync(b => b.Id == _main.Id);
        await Matcher().PinAsync(budget, [imported.BusinessId], default);

        Assert.Equal(_savings.BusinessId, await PinOf(imported));
    }

    [Fact]
    public async Task Zdjecie_powiazania_czysci_przypiecia_i_reguly()
    {
        Add(new DateOnly(2026, 9, 2), -500m, "PRZELEW WLASNY");
        await _db.SaveChangesAsync();
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);

        await UpdateLink().HandleAsync(_main.BusinessId, new UpdateSavingsLinkRequestDto(null, []), default);

        _db.ChangeTracker.Clear();
        var reloaded = await _db.Budgets.SingleAsync(b => b.Id == _main.Id);
        Assert.Null(reloaded.LinkedSavingsBudgetBusinessId);
        Assert.Empty(reloaded.SavingsTransferRules);
        Assert.All(await _db.Transactions.Where(t => t.BudgetBusinessId == _main.BusinessId).ToListAsync(),
            t => Assert.Null(t.SavingsTransferBudgetBusinessId));
    }

    // ── Walidacja ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Walidacja_odrzuca_link_do_samego_siebie_nieznany_cel_i_zle_reguly()
    {
        await Assert.ThrowsAsync<SavingsLinkTargetInvalidException>(
            () => UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_main.BusinessId), default));
        await Assert.ThrowsAsync<SavingsLinkTargetInvalidException>(
            () => UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<SavingsTransferRulesInvalidException>(
            () => UpdateLink().HandleAsync(_main.BusinessId, new UpdateSavingsLinkRequestDto(_savings.BusinessId, []), default));
        await Assert.ThrowsAsync<SavingsTransferPatternInvalidException>(
            () => UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId, pattern: "x"), default));
        await Assert.ThrowsAsync<SavingsTransferAmountInvalidException>(
            () => UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId, from: 100m, to: 50m), default));
    }

    [Fact]
    public async Task Odpiecie_nieprzypietej_transakcji_to_404()
    {
        var t = Add(new DateOnly(2026, 9, 2), -50m, "ZWYKLY WYDATEK");
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<SavingsTransferPinNotFoundException>(() => Unpin().HandleAsync(t.BusinessId, default));
    }

    // ── Dashboard i lista transakcji ─────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_wyklucza_transfer_z_sum_ale_nie_z_bilansu()
    {
        Add(new DateOnly(2026, 9, 2), -500m, "PRZELEW WLASNY");
        Add(new DateOnly(2026, 9, 3), -80m, "BIEDRONKA", category: _groceries);
        Add(new DateOnly(2026, 9, 4), 3000m, "WYNAGRODZENIE");
        await _db.SaveChangesAsync();
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);

        var handler = new GetDashboardQueryHandler(_db, Localizer());
        var response = await handler.HandleAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), _main.BusinessId, default);

        // Suma wydatków widzi TYLKO Biedronkę — przelew na oszczędności jest wykluczony.
        Assert.Equal(80m, response.Metrics.TotalExpenses);
        Assert.Equal(3000m, response.Metrics.TotalIncome);
        // Bilans WIDZI przelew: 1000 (start) - 500 - 80 + 3000 = 3420.
        Assert.Equal(3420m, response.Metrics.BudgetBalance);
    }

    [Fact]
    public async Task Lista_transakcji_pokazuje_transfer_w_wierszach_ale_nie_w_sumach()
    {
        var transfer = Add(new DateOnly(2026, 9, 2), -500m, "PRZELEW WLASNY");
        Add(new DateOnly(2026, 9, 3), -80m, "BIEDRONKA", category: _groceries);
        await _db.SaveChangesAsync();
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);

        var handler = new GetTransactionsListQueryHandler(
            new TransactionBudgetScope(_db, _clock), new TransactionFilters(_db), new TransactionListItemReader(_db));
        var filter = new TransactionFilterRequestDto(
            [_main.BusinessId], null, null, null, false, TransactionDirection.All, null, null, null, null);

        var response = await handler.HandleAsync(filter, 1, 20, null, true, default);

        // Historia 1:1 z bankiem — obie transakcje są w wierszach.
        Assert.Equal(2, response.Items.Count);
        Assert.Contains(response.Items, i => i.Id == transfer.BusinessId && i.SavingsTransferBudgetId == _savings.BusinessId);
        // Suma wydatków widzi TYLKO Biedronkę.
        Assert.Equal(80m, response.Summary.TotalExpenses);
    }

    // ── Stan konta oszczędnościowego ─────────────────────────────────────────────────────

    [Fact]
    public async Task Stan_konta_dla_powiazanego_budzetu_liczy_sie_z_bilansu_nie_z_kategorii()
    {
        // Wpłata i wypłata, żadna nie ma kategorii "Oszczędności" — fallback po kategorii dałby zero.
        Add(new DateOnly(2026, 9, 2), 1500m, "WPLATA", budget: _savings);
        Add(new DateOnly(2026, 9, 5), -300m, "WYPLATA", budget: _savings);
        await _db.SaveChangesAsync();
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);

        var account = new SavingsAccount(_db, new SavingsCategory(_db));
        var balance = await account.BalanceAsync([_main.BusinessId], default);

        // Bilans budżetu oszczędnościowego: 0 (start) + 1500 - 300 = 1200.
        Assert.Equal(1200m, balance);
    }

    [Fact]
    public async Task Usuniecie_powiazanego_budzetu_czysci_link_na_glownym()
    {
        Add(new DateOnly(2026, 9, 2), -500m, "PRZELEW WLASNY");
        await _db.SaveChangesAsync();
        await UpdateLink().HandleAsync(_main.BusinessId, LinkRequest(_savings.BusinessId), default);

        await DeleteBudget().HandleAsync(_savings.BusinessId, default);

        _db.ChangeTracker.Clear();
        var reloaded = await _db.Budgets.SingleAsync(b => b.Id == _main.Id);
        Assert.Null(reloaded.LinkedSavingsBudgetBusinessId);
        Assert.Empty(reloaded.SavingsTransferRules);
    }

    [Fact]
    public async Task Tworzenie_budzetu_z_opcja_zakada_powiazany_budzet_oszczednosciowy_jedna_operacja()
    {
        var handler = new CreateBudgetCommandHandler(_db, _clock);
        if (!await _db.Currencies.AnyAsync(c => c.Code == "PLN"))
        {
            _db.Currencies.Add(new Currency("PLN"));
            await _db.SaveChangesAsync();
        }

        var (created, error) = await handler.HandleAsync(
            new CreateBudgetRequestDto("Wakacje", "PLN", 0m, "Wakacje — Oszczędności"), default);

        Assert.Equal(BudgetTracker.Api.Features.Budgets.Consts.CreateBudgetError.None, error);
        Assert.NotNull(created!.LinkedSavingsBudgetId);

        var linked = await _db.Budgets.SingleAsync(b => b.BusinessId == created.LinkedSavingsBudgetId);
        Assert.Equal("Wakacje — Oszczędności", linked.Name);
    }

    private static IStringLocalizer<SharedResource> Localizer()
    {
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()),
            NullLoggerFactory.Instance);
        return new StringLocalizer<SharedResource>(factory);
    }
}
