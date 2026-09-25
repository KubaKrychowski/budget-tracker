using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Search.Consts;
using BudgetTracker.Api.Features.Search.Contracts;
using BudgetTracker.Api.Features.Search.Queries;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Wyszukiwarka z nagłówka: zakres budżetu, grupowanie i to, że liczniki mówią o CAŁOŚCI, nie o podglądzie.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class SearchTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_search_test;Username=budget;Password=budget_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero));

    private AppDbContext _db = null!;
    private Guid _mainId;
    private Guid _savingsId;
    private Category _catering = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _catering = new Category("Catering");
        _db.Categories.AddRange(_catering, new Category("Jedzenie"));

        var main = new Budget("Budżet Główny", new DateOnly(2026, 1, 1), 0m, default);
        var savings = new Budget("Konto Oszczędnościowe", new DateOnly(2026, 1, 1), 0m, default);
        _db.Budgets.AddRange(main, savings);
        await _db.SaveChangesAsync();

        _mainId = main.BusinessId;
        _savingsId = savings.BusinessId;
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private GetSearchResultsQueryHandler Query() => new(_db, _clock);

    private Task<SearchResponseDto> SearchAsync(string q, Guid? budgetId = null, bool allBudgets = false) =>
        Query().HandleAsync(q, budgetId ?? _mainId, allBudgets, default);

    private void Spend(Guid budgetId, string description, decimal amount, int day = 10, Category? category = null) =>
        _db.Transactions.Add(new Transaction(
            new DateOnly(2026, 8, day),
            -amount,
            description,
            default,
            TransactionStatus.Confirmed,
            categoryId: category?.Id,
            budgetBusinessId: budgetId));

    private static SearchGroupResponseDto? Group(SearchResponseDto result, string kind) =>
        result.Groups.FirstOrDefault(g => g.Kind == kind);

    // ── Zakres ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Domyslnie_szuka_w_JEDNYM_budzecie_a_odpowiedz_mowi_w_ktorym()
    {
        Spend(_mainId, "CATERING PUDELKOWY", 1200m);
        Spend(_savingsId, "CATERING PUDELKOWY przelew", 100m);
        await _db.SaveChangesAsync();

        var result = await SearchAsync("catering p");

        Assert.Equal(1, Group(result, SearchKind.Transactions)!.Total);
        Assert.Equal(_mainId, result.BudgetId);
        Assert.Equal("Budżet Główny", result.BudgetName);
        Assert.False(result.AllBudgets);
    }

    [Fact]
    public async Task Poszerzenie_na_wszystkie_budzety_dokłada_konto_oszczednosciowe()
    {
        Spend(_mainId, "CATERING PUDELKOWY", 1200m);
        Spend(_savingsId, "CATERING PUDELKOWY przelew", 100m);
        await _db.SaveChangesAsync();

        var result = await SearchAsync("catering p", allBudgets: true);

        Assert.Equal(2, Group(result, SearchKind.Transactions)!.Total);
        Assert.Null(result.BudgetId);
        Assert.True(result.AllBudgets);
    }

    [Fact]
    public async Task Kategorie_nie_naleza_do_budzetu_wiec_nie_sa_zawezane()
    {
        var result = await SearchAsync("cater");

        var categories = Group(result, SearchKind.Categories);
        Assert.NotNull(categories);
        Assert.Equal("Catering", categories!.Hits.Single().Label);
        Assert.Null(categories.Hits.Single().BudgetId);
    }

    // ── Liczniki i podgląd ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Licznik_grupy_mowi_o_CALOSCI_a_podglad_jest_przyciety()
    {
        // Podgląd bez pełnego licznika wygląda jak komplet — użytkownik przestaje szukać przy piątym wyniku.
        for (var i = 1; i <= 9; i++) Spend(_mainId, $"BIEDRONKA {i}", 10m * i, day: i);
        await _db.SaveChangesAsync();

        var group = Group(await SearchAsync("biedronka"), SearchKind.Transactions)!;

        Assert.Equal(9, group.Total);
        Assert.Equal(GetSearchResultsQueryHandler.HitsPerGroup, group.Hits.Count);
    }

    [Fact]
    public async Task Transakcje_ida_od_najnowszej_bo_pyta_sie_o_ostatni_zakup()
    {
        Spend(_mainId, "BIEDRONKA stara", 10m, day: 1);
        Spend(_mainId, "BIEDRONKA nowa", 20m, day: 28);
        await _db.SaveChangesAsync();

        var group = Group(await SearchAsync("biedronka"), SearchKind.Transactions)!;

        Assert.Equal("BIEDRONKA nowa", group.Hits[0].Label);
    }

    [Fact]
    public async Task Trafienie_transakcji_niesie_date_kwote_i_kategorie()
    {
        Spend(_mainId, "CATERING PUDELKOWY", 1200m, day: 27, category: _catering);
        await _db.SaveChangesAsync();

        var hit = Group(await SearchAsync("catering p"), SearchKind.Transactions)!.Hits.Single();

        Assert.Equal(new DateOnly(2026, 8, 27), hit.Date);
        Assert.Equal(-1200m, hit.Amount);
        Assert.Equal("Catering", hit.CategoryName);
        Assert.Equal("Budżet Główny", hit.BudgetName);
    }

    // ── Fraza ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fraza_krotsza_niz_minimum_nie_szuka_niczego()
    {
        Spend(_mainId, "BIEDRONKA", 10m);
        await _db.SaveChangesAsync();

        var result = await SearchAsync("b");

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Procent_we_frazie_jest_znakiem_a_nie_metaznakiem_wzorca()
    {
        // Bez ucieczki wzorzec „%5%%" znaczy po prostu „zawiera 5" i wciąga ODSETKI jako trafienie.
        Spend(_mainId, "PROWIZJA 5% OD KWOTY", 50m);
        Spend(_mainId, "ODSETKI 15 PLN", 15m);
        await _db.SaveChangesAsync();

        var group = Group(await SearchAsync("5%"), SearchKind.Transactions)!;

        Assert.Equal(1, group.Total);
        Assert.Equal("PROWIZJA 5% OD KWOTY", group.Hits.Single().Label);
    }

    [Fact]
    public async Task Puste_grupy_nie_wchodza_do_odpowiedzi()
    {
        Spend(_mainId, "BIEDRONKA", 10m);
        await _db.SaveChangesAsync();

        var result = await SearchAsync("biedronka");

        Assert.Single(result.Groups);
        Assert.Equal(SearchKind.Transactions, result.Groups[0].Kind);
    }

    [Fact]
    public async Task Grupy_ida_w_stalej_kolejnosci_a_transakcje_na_koncu()
    {
        // Transakcji jest najwięcej; na początku zepchnęłyby kategorię i zlecenie poza widok menu.
        Spend(_mainId, "CATERING dieta", 100m);
        _db.StandingOrders.Add(new StandingOrder(
            _mainId, "Catering miesięczny", 1500m, StandingOrderRhythm.Monthly, null, _clock.GetUtcNow()));
        await _db.SaveChangesAsync();

        var kinds = (await SearchAsync("cater")).Groups.Select(g => g.Kind).ToList();

        Assert.Equal([SearchKind.Categories, SearchKind.StandingOrders, SearchKind.Transactions], kinds);
    }
}
