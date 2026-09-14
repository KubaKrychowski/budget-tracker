using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Limits.Commands;
using BudgetTracker.Api.Features.Limits.Consts;
using BudgetTracker.Api.Features.Limits.Contracts;
using BudgetTracker.Api.Features.Limits.Exceptions;
using BudgetTracker.Api.Features.Limits.Queries;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Limity wydatków: historia limitów, stan paska i to, co się liczy do limitu.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class LimitsTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_limits_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś" to 15 września 2026 — sierpień jest zamknięty, październik w przyszłości.</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly August = new(2026, 8, 1);
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateOnly October = new(2026, 10, 1);

    private AppDbContext _db = null!;
    private Guid _budgetId;
    private Category _food = null!;
    private Category _fun = null!;
    private Category _salary = null!;
    private Category _savings = null!;

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
        _fun = new Category("Rozrywka");
        _salary = new Category("Wynagrodzenie");
        _savings = new Category("Oszczędności");
        _db.Categories.AddRange(_food, _fun, _salary, _savings);

        var budget = new Budget("Domowy", new DateOnly(2026, 1, 1), 0m, default);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        // Wynagrodzenie jest przychodowe, bo WSZYSTKIE jego reguły dotyczą wyłącznie wpływów.
        _db.CategoryRules.Add(new CategoryRule(_salary.Id, RuleDirection.Income, 1, pattern: "^wyplata"));
        await _db.SaveChangesAsync();

        _budgetId = budget.BusinessId;
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private LimitsBudgetScope Scope() => new(_db, _clock);

    private GetLimitsQueryHandler Query() => new(_db, Scope(), new LimitCategories(_db));

    private SetLimitCommandHandler Set() => new(_db, Scope(), new LimitCategories(_db));

    private RemoveLimitCommandHandler Remove() => new(_db, Scope());

    private Task<LimitSavedResponseDto> SetAsync(Category category, decimal amount, DateOnly from, int threshold = 80) =>
        Set().HandleAsync(new SetLimitRequestDto(_budgetId, category.BusinessId, amount, threshold, from), default);

    private void Spend(Category? category, DateOnly month, decimal amount, bool large = false) =>
        _db.Transactions.Add(new Transaction(
            new DateOnly(month.Year, month.Month, 10),
            -amount,
            "TEST",
            default,
            TransactionStatus.Confirmed,
            categoryId: category?.Id,
            isLargeExpense: large,
            budgetBusinessId: _budgetId));

    private async Task<LimitRowResponseDto> RowAsync(Category category, DateOnly month) =>
        (await Query().HandleAsync(_budgetId, month, default)).Limits.Single(r => r.CategoryId == category.BusinessId);

    // ── Historia ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Miniony_miesiac_liczy_sie_z_limitem_ktory_WTEDY_obowiazywal()
    {
        // Sedno historii: dzisiejsza kwota przyłożona do sierpnia pokazałaby przekroczenie, którego nie było.
        await SetAsync(_food, 1000m, August);
        await SetAsync(_food, 1200m, September);

        var august = await RowAsync(_food, August);
        var september = await RowAsync(_food, September);

        Assert.Equal(1000m, august.Limit);
        Assert.Equal(1200m, august.NextLimit);
        Assert.Equal(September, august.NextValidFrom);
        Assert.Equal(1200m, september.Limit);
        Assert.Null(september.NextLimit);
    }

    [Fact]
    public async Task Zmiana_wstecz_istniejacego_limitu_to_409()
    {
        await SetAsync(_food, 1000m, August);

        await Assert.ThrowsAsync<LimitHistoryLockedException>(() => SetAsync(_food, 1500m, August));
        Assert.Equal(1000m, (await RowAsync(_food, August)).Limit);
    }

    [Fact]
    public async Task Pierwszy_limit_kategorii_wolno_ustawic_wstecz()
    {
        // Bez tego cała dotychczasowa historia zostaje „bez limitu" i ekran nie ma czego pokazać.
        await SetAsync(_food, 1000m, new DateOnly(2026, 3, 1));

        Assert.Equal(1000m, (await RowAsync(_food, new DateOnly(2026, 5, 1))).Limit);
    }

    [Fact]
    public async Task Zmiana_w_tym_samym_miesiacu_poprawia_limit_zamiast_dokladac_wiersz()
    {
        await SetAsync(_food, 1000m, September);
        await SetAsync(_food, 1100m, September, threshold: 90);

        var items = await _db.BudgetItems.ToListAsync();
        var only = Assert.Single(items);
        Assert.Equal(1100m, only.Limit);
        Assert.Equal(90, only.WarningThreshold);
    }

    [Fact]
    public async Task Wycofanie_zaplanowanej_zmiany_nie_zabiera_kategorii_limitu()
    {
        // ⚠️ Zaplanowana październikowa zmiana KOŃCZYŁA wrześniowy limit. Nowa decyzja od września z tą samą kwotą
        // kasuje październik — bez ponownego otwarcia wrzesień skończyłby się we wrześniu i kategoria straciłaby limit.
        await SetAsync(_food, 1000m, August);
        await SetAsync(_food, 1500m, October);

        await SetAsync(_food, 1000m, September);

        Assert.Equal(1000m, (await RowAsync(_food, October)).Limit);
        Assert.Equal(1000m, (await RowAsync(_food, new DateOnly(2027, 1, 1))).Limit);
    }

    [Fact]
    public async Task Usuniecie_konczy_limit_na_poprzednim_miesiacu_a_historia_zostaje()
    {
        var saved = await SetAsync(_food, 1000m, August);

        await Remove().HandleAsync(saved.Id, default);

        Assert.Equal(1000m, (await RowAsync(_food, August)).Limit);
        Assert.Empty((await Query().HandleAsync(_budgetId, September, default)).Limits);
    }

    [Fact]
    public async Task Usuniecie_limitu_ktory_nie_objal_zamknietego_miesiaca_kasuje_go()
    {
        var saved = await SetAsync(_food, 1000m, September);

        await Remove().HandleAsync(saved.Id, default);

        Assert.Empty(await _db.BudgetItems.ToListAsync());
    }

    [Fact]
    public async Task Usuniecie_limitu_zakonczonego_przed_biezacym_miesiacem_to_409()
    {
        var old = await SetAsync(_food, 1000m, August);
        await SetAsync(_food, 1200m, September);

        await Assert.ThrowsAsync<LimitHistoryLockedException>(() => Remove().HandleAsync(old.Id, default));
    }

    // ── Co się liczy ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Duze_wydatki_licza_sie_do_limitu_a_wydatki_bez_kategorii_nie()
    {
        await SetAsync(_fun, 350m, September);
        Spend(_fun, September, 300m);
        Spend(_fun, September, 120m, large: true);
        Spend(null, September, 310m);
        Spend(_fun, August, 999m);
        await _db.SaveChangesAsync();

        var response = await Query().HandleAsync(_budgetId, null, default);
        var row = Assert.Single(response.Limits);

        Assert.Equal(420m, row.Spent);
        Assert.Equal(-70m, row.Remaining);
        Assert.Equal(120, row.Percent);
        Assert.Equal(LimitState.Over, row.State);
        Assert.Equal(1, response.OverLimitCount);
        Assert.Equal(1, response.UncategorizedCount);
        Assert.Equal(310m, response.UncategorizedAmount);
    }

    [Fact]
    public async Task Prog_ostrzezenia_liczony_na_kwotach_a_nie_na_zaokraglonym_procencie()
    {
        // 796 z 1000 to 79,6% — zaokrąglone do 80 zapaliłoby ostrzeżenie przy progu 80, choć próg nie padł.
        await SetAsync(_food, 1000m, September, threshold: 80);
        Spend(_food, September, 796m);
        await _db.SaveChangesAsync();

        var row = await RowAsync(_food, September);

        Assert.Equal(80, row.Percent);
        Assert.Equal(LimitState.Ok, row.State);
    }

    [Fact]
    public async Task Prog_ostrzezenia_jest_per_limit()
    {
        await SetAsync(_food, 1000m, September, threshold: 75);
        Spend(_food, September, 780m);
        await _db.SaveChangesAsync();

        Assert.Equal(LimitState.Warning, (await RowAsync(_food, September)).State);
    }

    [Fact]
    public async Task Wydatki_bez_limitu_nie_obejmuja_oszczednosci()
    {
        // Przelew na oszczędności to przesunięcie pieniędzy, nie wydatek — zawyżałby „wydane poza limitami".
        Spend(_fun, September, 200m);
        Spend(_savings, September, 1500m);
        await _db.SaveChangesAsync();

        var response = await Query().HandleAsync(_budgetId, null, default);

        var unlimited = Assert.Single(response.Unlimited);
        Assert.Equal("Rozrywka", unlimited.CategoryName);
        Assert.Equal(200m, response.SpentOutside);
    }

    [Fact]
    public async Task Limitu_nie_da_sie_nalozyc_na_kategorie_przychodowa_ani_oszczednosci()
    {
        await Assert.ThrowsAsync<LimitCategoryInvalidException>(() => SetAsync(_salary, 100m, September));
        await Assert.ThrowsAsync<LimitCategoryInvalidException>(() => SetAsync(_savings, 100m, September));

        var options = (await Query().HandleAsync(_budgetId, null, default)).Categories.Select(c => c.Name);
        Assert.Equal(["Jedzenie", "Rozrywka"], options);
    }

    [Fact]
    public async Task Limit_budzetu_to_suma_limitow_obowiazujacych_w_miesiacu()
    {
        await SetAsync(_food, 1000m, August);
        await SetAsync(_food, 1200m, September);
        await SetAsync(_fun, 350m, September);

        Assert.Equal(1000m, (await Query().HandleAsync(_budgetId, August, default)).LimitTotal);
        Assert.Equal(1550m, (await Query().HandleAsync(_budgetId, September, default)).LimitTotal);
    }

    [Fact]
    public async Task Zamkniety_miesiac_jest_tylko_do_odczytu()
    {
        Assert.True((await Query().HandleAsync(_budgetId, August, default)).ReadOnly);
        Assert.False((await Query().HandleAsync(_budgetId, null, default)).ReadOnly);
    }

    // ── Walidacja i zasięg ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Prog_poza_zakresem_to_blad(int threshold)
    {
        await Assert.ThrowsAsync<LimitWarningThresholdInvalidException>(
            () => SetAsync(_food, 1000m, September, threshold));
    }

    [Fact]
    public async Task Limit_zero_to_blad()
    {
        await Assert.ThrowsAsync<LimitAmountInvalidException>(() => SetAsync(_food, 0m, September));
    }

    [Fact]
    public async Task Nieznany_budzet_to_404()
    {
        await Assert.ThrowsAsync<BudgetNotFoundException>(() => Query().HandleAsync(Guid.NewGuid(), null, default));
    }

    [Fact]
    public void Stan_limitu_jedzie_do_frontu_jako_NAZWA()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(LimitState.Warning);
        Assert.Equal("\"Warning\"", json);
    }
}
