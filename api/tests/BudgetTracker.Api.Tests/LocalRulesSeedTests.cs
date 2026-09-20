using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Reguły z pliku poza repozytorium (<c>data/category-rules.json</c>).
///
/// <para>
/// Plik jest konfiguracją środowiska edytowaną ręcznie, więc testy pilnują dwóch rzeczy naraz:
/// że działa przy każdym starcie bez dokładania duplikatów, i że literówka w nim kosztuje
/// wpis w logu, a nie martwą aplikację.
/// </para>
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class LocalRulesSeedTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_localrules_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"bt-local-rules-{Guid.NewGuid():N}.json");

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
    }

    public async Task DisposeAsync()
    {
        if (File.Exists(_path)) File.Delete(_path);
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private Task SeedAsync() => LocalRulesSeed.SeedAsync(_db, _path, NullLogger.Instance);

    private Task<int> ActiveRuleCountAsync() => _db.Set<CategoryRule>().CountAsync();

    [Fact]
    public async Task Missing_file_is_a_normal_state_not_an_error()
    {
        var before = await ActiveRuleCountAsync();

        await SeedAsync();

        Assert.Equal(before, await ActiveRuleCountAsync());
    }

    [Fact]
    public async Task Loads_rules_and_adds_no_duplicates_on_the_next_start()
    {
        // Komentarz w pliku celowo — prawdziwy plik jest nimi opisany, a parser ma to przełknąć.
        await File.WriteAllTextAsync(_path, """
            // reguły tej instalacji
            [
              { "category": "Zdrowie", "pattern": "gabinet pod lipa", "priority": 170 },
              { "category": "Paliwo", "pattern": "stacja rondo", "direction": "Expense", "priority": 120, "minAmount": 50.00 },
            ]
            """);
        var before = await ActiveRuleCountAsync();

        await SeedAsync();
        await SeedAsync(); // drugi start aplikacji

        Assert.Equal(before + 2, await ActiveRuleCountAsync());

        var fuel = await _db.Set<CategoryRule>().SingleAsync(r => r.Pattern == "stacja rondo");
        Assert.Equal(RuleDirection.Expense, fuel.Direction);
        Assert.Equal(50.00m, fuel.MinAmount);
    }

    [Fact]
    public async Task Broken_json_does_not_stop_the_application()
    {
        await File.WriteAllTextAsync(_path, """[ { "category": "Zdrowie", "pattern": """);
        var before = await ActiveRuleCountAsync();

        await SeedAsync();

        Assert.Equal(before, await ActiveRuleCountAsync());
    }

    [Fact]
    public async Task Skips_entries_with_unknown_category_or_without_any_pattern()
    {
        await File.WriteAllTextAsync(_path, """
            [
              { "category": "Kategoria której nie ma", "pattern": "cokolwiek" },
              { "category": "Hobby" }
            ]
            """);
        var before = await ActiveRuleCountAsync();

        await SeedAsync();

        Assert.Equal(before, await ActiveRuleCountAsync());
    }

    [Fact]
    public async Task Does_not_resurrect_a_rule_that_was_deleted()
    {
        // ⚠️ Bez tego usunięcie reguły byłoby pozorne: plik nadal ją zawiera, więc przy następnym starcie wróciłaby
        // jako nowy wiersz. Seed patrzy więc także na skasowane. Reguły z pliku są WSPÓLNE (tylko do odczytu dla użytkowników,
        // API zwraca 409), więc skasowanie robimy wprost — obojętne, kto je wykonuje, liczy się to, że wiersz zostaje.
        await File.WriteAllTextAsync(_path, """[ { "category": "Hobby", "pattern": "kolejka modeli", "priority": 180 } ]""");
        await SeedAsync();

        var rule = await _db.Set<CategoryRule>().SingleAsync(r => r.Pattern == "kolejka modeli");
        _db.Set<CategoryRule>().Remove(rule);
        await _db.SaveChangesAsync();

        await SeedAsync(); // restart po usunięciu

        Assert.False(await _db.Set<CategoryRule>().AnyAsync(r => r.Pattern == "kolejka modeli"));
        Assert.Equal(1, await _db.Set<CategoryRule>().IgnoreQueryFilters().CountAsync(r => r.Pattern == "kolejka modeli"));
    }
}
