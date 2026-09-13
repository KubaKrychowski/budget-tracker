using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Podgląd trafień reguły w kreatorze — „co ta reguła złapie", bez zapisywania czegokolwiek.
///
/// <para>
/// ⚠️ Najważniejszy test w tym pliku to <see cref="Matches_exactly_what_the_engine_would_match"/>.
/// Podgląd i kategoryzacja MUSZĄ dawać ten sam wynik, bo użytkownik zapisuje regułę w zaufaniu
/// do podglądu. Reszta testów pilnuje warunków brzegowych tego samego dopasowania.
/// </para>
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class CategoryRulePreviewTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_rulepreview_test;Username=budget;Password=budget_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
    private AppDbContext _db = null!;
    private Guid _fuel;
    private Guid _cash;

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

        // ⚠️ Reguły produkcyjne z seeda są tu szumem: test o przesłonięciu musi wiedzieć DOKŁADNIE,
        // co stoi przed regułą kandydatem. Kasujemy je fizycznie, bo soft delete zostawiłby je
        // widoczne dla zapytań z IgnoreQueryFilters.
        await _db.Set<CategoryRule>().IgnoreQueryFilters().ExecuteDeleteAsync();

        _fuel = await _db.Categories.Where(c => c.Name == "Paliwo").Select(c => c.BusinessId).SingleAsync();
        _cash = await _db.Categories.Where(c => c.Name == "Gotówka").Select(c => c.BusinessId).SingleAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private PreviewCategoryRuleQueryHandler Preview() => new(_db, new CategoryRuleLookup(_db), _clock);

    private CategoryRuleRequestDto Request(
        string? pattern = "STACJA PALIW", string? type = null, Guid? category = null,
        RuleDirection direction = RuleDirection.Expense, int priority = 100,
        decimal? min = null, decimal? max = null) =>
        new(pattern, type, direction, category ?? _fuel, priority, min, max, null);

    private async Task AddTransactionAsync(
        string description, decimal amount, int monthsAgo = 1, string type = "Obciążenie")
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime.Date);
        _db.Transactions.Add(new Transaction(
            today.AddMonths(-monthsAgo),
            amount,
            description,
            _clock.GetUtcNow(),
            TransactionStatus.Imported,
            transactionType: type));
        await _db.SaveChangesAsync();
    }

    private async Task AddRuleAsync(string? pattern, Guid category, int priority, string? type = null)
    {
        var id = await _db.Categories.Where(c => c.BusinessId == category).Select(c => c.Id).SingleAsync();
        _db.Set<CategoryRule>().Add(new CategoryRule(
            id, RuleDirection.Expense, priority, pattern, type));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Counts_matches_and_says_how_many_transactions_were_scanned()
    {
        await AddTransactionAsync("STACJA PALIW RONDO 07", -187.40m);
        await AddTransactionAsync("STACJA PALIW A4", -301.15m);
        await AddTransactionAsync("SKLEP SPOZYWCZY KALINA 12", -64.10m);

        var result = await Preview().HandleAsync(Request(), default);

        Assert.Equal(2, result.MatchCount);
        Assert.Equal(3, result.ScannedCount);
        Assert.Equal(0, result.ShadowedCount);
        Assert.Equal(2, result.Samples.Count);
    }

    [Fact]
    public async Task Saves_nothing()
    {
        await AddTransactionAsync("STACJA PALIW RONDO 07", -187.40m);

        await Preview().HandleAsync(Request(), default);

        Assert.Empty(await _db.Set<CategoryRule>().IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Matches_exactly_what_the_engine_would_match()
    {
        // ⚠️ Sedno tej funkcji. Gdyby podgląd miał własną kopię warunków dopasowania, ten test
        // byłby jedynym miejscem, które złapie rozjazd — dlatego porównuje wynik podglądu
        // z wynikiem PRAWDZIWEJ kategoryzacji po zapisaniu tej samej reguły.
        var samples = new[]
        {
            ("STACJA PALIW RONDO 07", -187.40m, "Obciążenie"),
            ("PALIWA KRESKA", -256.00m, "Obciążenie"),
            ("ZWROT STACJA PALIW RONDO", 45.00m, "Zwrot w terminalu"),
            ("SKLEP SPOZYWCZY KALINA 12", -64.10m, "Obciążenie"),
            ("BANKOMAT PRZY STACJA PALIW", -500.00m, "Wypłata w bankomacie"),
        };
        foreach (var (desc, amount, type) in samples) await AddTransactionAsync(desc, amount, 1, type);

        var request = Request(pattern: "STACJA PALIW|PALIWA");
        var preview = await Preview().HandleAsync(request, default);

        var fuelId = await _db.Categories.Where(c => c.BusinessId == _fuel).Select(c => c.Id).SingleAsync();
        await AddRuleAsync("STACJA PALIW|PALIWA", _fuel, 100);

        var categorizer = new RuleCategorizer(_db);
        var engineHits = 0;
        foreach (var (desc, amount, type) in samples)
        {
            var suggestion = await categorizer.CategorizeAsync(
                DescriptionNormalizer.Normalize(desc), type, amount, default);
            if (suggestion.CategoryId == fuelId) engineHits++;
        }

        Assert.Equal(engineHits, preview.MatchCount);
        Assert.Equal(3, preview.MatchCount); // dwa paliwa + bankomat; zwrot odpada na kierunku
    }

    [Fact]
    public async Task Reports_matches_already_taken_by_an_earlier_rule_as_shadowed()
    {
        // Reguła może łapać poprawnie i nie robić NIC, bo wcześniejsza (niższy priorytet) bierze
        // te transakcje pierwsza. Bez tej liczby podgląd obiecywałby skutek, którego reguła nie ma.
        await AddTransactionAsync("BANKOMAT PRZY STACJA PALIW", -500.00m, type: "Wypłata w bankomacie");
        await AddTransactionAsync("STACJA PALIW A4", -301.15m);
        // Wzorzec jak w BaselineSeed: „bankomat" NIE łapie „Wypłata w bankomacie", bo typ operacji
        // przychodzi odmieniony. To nie jest szczegół testu — to powód, dla którego produkcyjna
        // reguła ma alternatywę z obiema formami.
        await AddRuleAsync(null, _cash, 5, type: "bankomac|bankomat");

        var result = await Preview().HandleAsync(Request(pattern: "STACJA PALIW", priority: 100), default);

        Assert.Equal(2, result.MatchCount);
        Assert.Equal(1, result.ShadowedCount);
        Assert.DoesNotContain(result.Samples, s => s.Description.StartsWith("BANKOMAT"));
    }

    [Fact]
    public async Task Treats_an_equal_priority_rule_as_earlier_because_a_new_rule_gets_the_highest_id()
    {
        // Kolejność to priorytet, potem Id — nowy wiersz dostaje najwyższe Id, więc przy remisie
        // przegrywa z tym, co już jest. Podgląd musi zakładać to samo, inaczej zawyża skutek.
        await AddTransactionAsync("STACJA PALIW A4", -301.15m);
        await AddRuleAsync("STACJA PALIW", _cash, 100);

        var result = await Preview().HandleAsync(Request(priority: 100), default);

        Assert.Equal(1, result.MatchCount);
        Assert.Equal(1, result.ShadowedCount);
        Assert.NotEmpty(result.Samples); // przykłady są nawet gdy wszystko jest przesłonięte
    }

    [Fact]
    public async Task Applies_direction_to_the_sign_and_the_range_to_the_absolute_value()
    {
        await AddTransactionAsync("ABONAMENT", -150.00m);
        await AddTransactionAsync("ABONAMENT ZWROT", 150.00m);

        var expense = await Preview().HandleAsync(
            Request(pattern: "ABONAMENT", direction: RuleDirection.Expense, min: 100m, max: 200m), default);
        Assert.Equal(1, expense.MatchCount);

        // Górna granica jest WYŁĄCZNA — 150 przy max 150 nie łapie.
        var exclusive = await Preview().HandleAsync(
            Request(pattern: "ABONAMENT", direction: RuleDirection.Expense, min: 100m, max: 150m), default);
        Assert.Equal(0, exclusive.MatchCount);

        var any = await Preview().HandleAsync(
            Request(pattern: "ABONAMENT", direction: RuleDirection.Any), default);
        Assert.Equal(2, any.MatchCount);
    }

    [Fact]
    public async Task Ignores_transactions_older_than_the_window()
    {
        await AddTransactionAsync("STACJA PALIW A4", -301.15m, monthsAgo: 13);
        await AddTransactionAsync("STACJA PALIW RONDO 07", -187.40m, monthsAgo: 2);

        var result = await Preview().HandleAsync(Request(), default);

        Assert.Equal(1, result.MatchCount);
        Assert.Equal(1, result.ScannedCount);
    }

    [Fact]
    public async Task Rejects_exactly_what_saving_rejects()
    {
        // Podgląd nie może być łagodniejszy od zapisu — inaczej użytkownik widzi ładny wynik,
        // a „Zapisz" zwraca 400 albo 404 i wygląda to na usterkę zapisu, nie na błąd w regule.
        await Assert.ThrowsAsync<RulePatternRequiredException>(() =>
            Preview().HandleAsync(Request(pattern: null), default));

        await Assert.ThrowsAsync<RulePatternInvalidException>(() =>
            Preview().HandleAsync(Request(pattern: "STACJA ("), default));

        await Assert.ThrowsAsync<RuleAmountRangeInvalidException>(() =>
            Preview().HandleAsync(Request(min: 100m, max: 100m), default));

        await Assert.ThrowsAsync<CategoryNotFoundException>(() =>
            Preview().HandleAsync(Request(category: Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Shows_the_current_category_so_it_is_clear_what_the_rule_would_change()
    {
        var fuelId = await _db.Categories.Where(c => c.BusinessId == _fuel).Select(c => c.Id).SingleAsync();
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime.Date);
        _db.Transactions.Add(new Transaction(
            today.AddMonths(-1), -187.40m, "STACJA PALIW RONDO 07", _clock.GetUtcNow(),
            TransactionStatus.AutoCategorized, categoryId: fuelId, transactionType: "Obciążenie"));
        await _db.SaveChangesAsync();

        var result = await Preview().HandleAsync(Request(), default);

        Assert.Equal("Paliwo", Assert.Single(result.Samples).CurrentCategoryName);
    }
}
