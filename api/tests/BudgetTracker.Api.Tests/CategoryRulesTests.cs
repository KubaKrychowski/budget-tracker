using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Zarządzanie regułami przez API (issue #13).
///
/// <para>
/// Wspólny mianownik walidacji: każdy odrzucony tu przypadek to reguła, która zapisałaby się bez
/// błędu i po prostu nigdy nie zadziałała — a użytkownik nie miałby jak się dowiedzieć dlaczego,
/// bo <see cref="RuleCategorizer"/> takie wiersze po cichu pomija.
/// </para>
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class CategoryRulesTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_ruleshandler_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private readonly Guid _userId = Guid.CreateVersion7();
    private Guid _health;
    private Guid _hobby;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        await BaselineSeed.SeedAsync(_db);

        _health = await _db.Categories.Where(c => c.Name == "Zdrowie").Select(c => c.BusinessId).SingleAsync();
        _hobby = await _db.Categories.Where(c => c.Name == "Hobby").Select(c => c.BusinessId).SingleAsync();
    }

    private CreateCategoryRuleCommandHandler CreateHandler() =>
        new(_db, new CategoryRuleLookup(_db), new FakeCurrentUserAccessor(_userId));

    private UpdateCategoryRuleCommandHandler UpdateHandler() => new(_db, new CategoryRuleLookup(_db));

    private DeleteCategoryRuleCommandHandler DeleteHandler() => new(_db, new CategoryRuleLookup(_db));

    private GetCategoryRulesQueryHandler ListHandler() => new(_db);

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private CategoryRuleRequestDto Request(
        string? pattern = "gabinet pod lipa", string? type = null, Guid? category = null,
        int priority = 175, decimal? min = null, decimal? max = null) =>
        new(pattern, type, RuleDirection.Expense, category ?? _health, priority, min, max, "notatka");

    [Fact]
    public async Task A_rule_added_through_the_api_works_on_the_next_request_without_a_restart()
    {
        // ⚠️ Sedno kryterium „bez restartu" — i przypięcie założenia, na którym ono stoi.
        // RuleCategorizer jest Scoped i trzyma reguły w polu instancji: stara instancja (bieżące
        // żądanie) nowej reguły NIE widzi, nowa instancja (następne żądanie) — widzi. Gdyby ktoś
        // zmienił rejestrację na Singleton, reguła dodana przez API przestałaby działać do restartu,
        // a objaw wyglądałby jak błąd zapisu. Ten test wtedy padnie.
        var currentRequest = new RuleCategorizer(_db);
        Assert.Equal(CategorySuggestion.None,
            await currentRequest.CategorizeAsync("gabinet pod lipa wizyta", "", -150m, default));

        await CreateHandler().HandleAsync(Request(), default);

        var nextRequest = new RuleCategorizer(_db);
        var healthId = await _db.Categories.Where(c => c.BusinessId == _health).Select(c => c.Id).SingleAsync();
        Assert.Equal(healthId,
            (await nextRequest.CategorizeAsync("gabinet pod lipa wizyta", "", -150m, default)).CategoryId);
    }

    [Fact]
    public async Task A_rule_created_through_the_api_belongs_to_the_current_user()
    {
        // Bez właściciela reguła użytkownika trafiałaby do wspólnych (pusty UserId): widzieliby ją wszyscy i nikt by jej nie zmienił.
        var created = await CreateHandler().HandleAsync(Request(pattern: "moj gabinet"), default);

        var rule = await _db.Set<CategoryRule>().SingleAsync(r => r.BusinessId == created.Id);
        Assert.Equal(_userId, rule.UserId);
        Assert.False(rule.IsShared);
    }

    [Fact]
    public async Task A_shared_rule_can_be_neither_updated_nor_deleted_by_a_user()
    {
        // Reguły bazowe są wspólne: RLS odrzuciłby zapis w bazie zmianą 0 wierszy, czyli błędem 500. Handler odpowiada 409 wcześniej.
        var shared = await _db.Set<CategoryRule>().FirstAsync(r => r.UserId == CategoryRule.SharedUserId);

        await Assert.ThrowsAsync<CategoryRuleSharedReadOnlyException>(() =>
            UpdateHandler().HandleAsync(shared.BusinessId, Request(), default));
        await Assert.ThrowsAsync<CategoryRuleSharedReadOnlyException>(() =>
            DeleteHandler().HandleAsync(shared.BusinessId, default));
    }

    [Fact]
    public async Task Lists_rules_in_the_order_the_engine_checks_them()
    {
        // Lista posortowana inaczej niż silnik ukrywałaby jedyną rzecz, która przy nachodzących
        // wzorcach rozstrzyga wynik.
        await CreateHandler().HandleAsync(Request(pattern: "zzz pozno", priority: 9999), default);
        await CreateHandler().HandleAsync(Request(pattern: "aaa wczesnie", priority: 0), default);

        var rules = await ListHandler().HandleAsync(default);

        Assert.Equal("aaa wczesnie", rules[0].Pattern);
        Assert.Equal("zzz pozno", rules[^1].Pattern);
        Assert.Equal(rules.Select(r => r.Priority).Order(), rules.Select(r => r.Priority));
    }

    [Fact]
    public async Task Rejects_a_rule_without_any_pattern()
    {
        await Assert.ThrowsAsync<RulePatternRequiredException>(() =>
            CreateHandler().HandleAsync(Request(pattern: null, type: null), default));
    }

    [Fact]
    public async Task Treats_a_whitespace_pattern_as_missing()
    {
        // Inaczej „   " byłoby wzorcem, który pasuje do opisu ze spacją — czyli prawie do wszystkiego.
        await Assert.ThrowsAsync<RulePatternRequiredException>(() =>
            CreateHandler().HandleAsync(Request(pattern: "   ", type: ""), default));
    }

    [Fact]
    public async Task Rejects_an_invalid_regex_at_save_time_not_at_import()
    {
        // RuleCategorizer celowo połyka zły wzorzec przy imporcie, więc bez tej walidacji
        // błąd byłby całkowicie niewidoczny.
        await Assert.ThrowsAsync<RulePatternInvalidException>(() =>
            CreateHandler().HandleAsync(Request(pattern: "diet[ay"), default));
    }

    [Fact]
    public async Task Rejects_equal_amount_bounds_because_they_match_nothing()
    {
        // Silnik sprawdza `abs < min` i `abs >= max` — przy min == max odpada każda kwota.
        await Assert.ThrowsAsync<RuleAmountRangeInvalidException>(() =>
            CreateHandler().HandleAsync(Request(min: 50m, max: 50m), default));
    }

    [Fact]
    public async Task Unknown_category_and_unknown_rule_are_not_found()
    {
        await Assert.ThrowsAsync<CategoryNotFoundException>(() =>
            CreateHandler().HandleAsync(Request(category: Guid.NewGuid()), default));

        await Assert.ThrowsAsync<CategoryRuleNotFoundException>(() =>
            UpdateHandler().HandleAsync(Guid.NewGuid(), Request(), default));
    }

    [Fact]
    public async Task Update_changes_the_rule_including_its_category()
    {
        var created = await CreateHandler().HandleAsync(Request(), default);

        var updated = await UpdateHandler().HandleAsync(
            created.Id, Request(pattern: "szachy", category: _hobby, priority: 185), default);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("szachy", updated.Pattern);
        Assert.Equal("Hobby", updated.CategoryName);
    }

    [Fact]
    public async Task Delete_is_logical_so_the_rule_still_explains_past_categorization()
    {
        // Reguła bywa jedynym wyjaśnieniem, dlaczego setki transakcji mają daną kategorię —
        // twarde usunięcie zabierałoby tę odpowiedź razem z wierszem.
        var created = await CreateHandler().HandleAsync(Request(), default);

        await DeleteHandler().HandleAsync(created.Id, default);

        Assert.DoesNotContain(await ListHandler().HandleAsync(default), r => r.Id == created.Id);
        var row = await _db.Set<CategoryRule>().IgnoreQueryFilters().SingleAsync(r => r.BusinessId == created.Id);
        Assert.NotNull(row.DeletedAt);
    }
}
