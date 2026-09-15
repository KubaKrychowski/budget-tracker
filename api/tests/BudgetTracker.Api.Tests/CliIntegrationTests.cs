using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets;
using BudgetTracker.Api.Features.Budgets.Consts;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Features.Dashboard;
using BudgetTracker.Api.Features.EpisodicOrders;
using BudgetTracker.Api.Features.Limits;
using BudgetTracker.Api.Features.Savings;
using BudgetTracker.Api.Features.StandingOrders;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Rejestracja komend CLI (issue #25): jeden-dwa testy PER RZECZOWNIK, nie na każdą z ~60 komend —
/// dowodzą, że rejestracja faktycznie woła właściwy handler i zwraca to samo, co odpowiedni endpoint
/// REST. Logikę biznesową samych handlerów sprawdzają już inne klasy testowe (np.
/// <see cref="BudgetSettingsTests"/>, <see cref="EpisodicOrdersTests"/>).
///
/// ⚠️ Kontener DI budowany jest RAZEM z aplikacją (te same <c>Add&lt;Feature&gt;</c>), więc test
/// łapie też brakującą rejestrację usługi, której `new Handler(...)` w innych klasach nigdy by nie
/// wykrył. `model train/recategorize/activate` są świadomie pominięte — dotykają plików modelu ML.NET,
/// co sprawdzają już <c>CategoryModelTrainingTests</c>/<c>ModelStoreTests</c>; sam fakt rejestracji
/// tych trzech komend potwierdza `dotnet build` (CLI mapowanie odwołuje się do tych samych typów).
/// </summary>
public sealed class CliIntegrationTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_cli_test;Username=budget;Password=budget_dev_only;Pooling=false";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private AppDbContext _db = null!;
    private IServiceProvider _services = null!;
    private CliCommandDispatcher _dispatcher = null!;

    public async Task InitializeAsync()
    {
        await TestDatabase.ResetAsync(TestConnection);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton<TimeProvider>(_clock);
        services.AddLogging();
        services.AddLocalization();
        services.AddDashboard();
        services.AddCategorization(config);
        services.AddBudgets(config);
        services.AddTransactions();
        services.AddSavings();
        services.AddLimits();
        services.AddStandingOrders();
        services.AddEpisodicOrders();
        _services = services.BuildServiceProvider();

        var registry = new CliCommandRegistry()
            .MapDashboardCli()
            .MapCategorizationCli()
            .MapLimitsCli()
            .MapStandingOrdersCli()
            .MapEpisodicOrdersCli()
            .MapSavingsCli()
            .MapBudgetsCli()
            .MapTransactionsCli();
        _dispatcher = new CliCommandDispatcher(registry);
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private Task<IResult> Run(string line) => _dispatcher.ExecuteAsync(line, _services, CancellationToken.None);

    private static object? BodyOf(IResult result) => (result as IValueHttpResult)?.Value;

    private async Task<Budget> SeedBudgetAsync(string name = "CLI budżet")
    {
        var budget = new Budget(name, new DateOnly(2026, 9, 1), 1000m, _clock.GetUtcNow());
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
        return budget;
    }

    /// <summary>Ekran „Cele oszczędzania" jest zablokowany bez powiązanego budżetu oszczędnościowego (#10) — patrz DECISIONS.md.</summary>
    private async Task<Budget> SeedBudgetWithLinkedSavingsAsync(string name = "CLI budżet")
    {
        var budget = await SeedBudgetAsync(name);
        var savingsBudget = await SeedBudgetAsync($"{name} — Oszczędności");
        budget.LinkSavingsBudget(savingsBudget.BusinessId);
        await _db.SaveChangesAsync();
        return budget;
    }

    private async Task<Category> SeedCategoryAsync(string name = "CLI kategoria")
    {
        var category = new Category(name);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task Budget_create_i_list_widza_sie_nawzajem()
    {
        var created = BodyOf(await Run("budget create --name \"Testowy CLI\" --currency PLN --initial-balance 500"));
        Assert.NotNull(created);

        var list = Assert.IsAssignableFrom<Features.Budgets.Contracts.BudgetListResponseDto>(
            BodyOf(await Run("budget list")));
        Assert.Contains(list.Budgets, b => b.Name == "Testowy CLI");
    }

    [Fact]
    public async Task Budget_disable_zamyka_budzet_widoczny_na_liscie()
    {
        var budget = await SeedBudgetAsync();

        var disabled = Assert.IsAssignableFrom<Features.Budgets.Contracts.BudgetListItemResponseDto>(
            BodyOf(await Run($"budget disable {budget.BusinessId}")));

        Assert.Equal(BudgetStatus.Disabled, disabled.Status);
    }

    [Fact]
    public async Task Nieznany_budget_w_update_rzuca_wyjatek_ktory_leci_dalej_do_DomainExceptionHandler()
    {
        // Dispatcher CLI nie łapie wyjątków domenowych (patrz CliCommandDispatcherTests) — tu dowodzimy
        // tego na PRAWDZIWYM handlerze: BudgetNotFoundException (dziedziczy po EntityNotFoundException,
        // które łapie DomainExceptionHandler) ma polecieć aż stąd.
        await Assert.ThrowsAsync<Features.Budgets.Exceptions.BudgetNotFoundException>(() =>
            Run($"budget update {Guid.NewGuid()} --name X --initial-balance 0"));
    }

    [Fact]
    public async Task Transaction_list_widzi_zaimportowana_transakcje_budzetu()
    {
        var budget = await SeedBudgetAsync();
        _db.Transactions.Add(new Transaction(
            new DateOnly(2026, 9, 10), -42m, "Testowy wydatek CLI", _clock.GetUtcNow(), TransactionStatus.Imported,
            budgetBusinessId: budget.BusinessId));
        await _db.SaveChangesAsync();

        var list = Assert.IsAssignableFrom<Features.Transactions.Contracts.TransactionListResponseDto>(
            BodyOf(await Run($"transaction list --budget-id {budget.BusinessId}")));

        Assert.Contains(list.Items, t => t.Description == "Testowy wydatek CLI");
    }

    [Fact]
    public async Task Category_list_zwraca_zaseedowana_kategorie()
    {
        await SeedCategoryAsync("CLI - kategoria testowa");

        var categories = Assert.IsAssignableFrom<IReadOnlyList<Features.Transactions.Contracts.CategoryOptionResponseDto>>(
            BodyOf(await Run("category list")));

        Assert.Contains(categories, c => c.Name == "CLI - kategoria testowa");
    }

    [Fact]
    public async Task Dashboard_show_liczy_bilans_zaimportowanego_budzetu()
    {
        var budget = await SeedBudgetAsync();
        _db.Transactions.Add(new Transaction(
            new DateOnly(2026, 9, 10), 100m, "Wpływ testowy CLI", _clock.GetUtcNow(), TransactionStatus.Imported,
            budgetBusinessId: budget.BusinessId));
        await _db.SaveChangesAsync();

        var dashboard = Assert.IsAssignableFrom<Features.Dashboard.Contracts.DashboardResponseDto>(
            BodyOf(await Run($"dashboard show --budget-id {budget.BusinessId}")));

        Assert.Equal(1100m, dashboard.Metrics.BudgetBalance);
    }

    [Fact]
    public async Task Limit_set_i_list_widza_sie_nawzajem()
    {
        var budget = await SeedBudgetAsync();
        var category = await SeedCategoryAsync();

        await Run(
            $"limit set --budget-id {budget.BusinessId} --category-id {category.BusinessId} "
            + "--amount 300 --warning-threshold 80 --valid-from 2026-09-01");

        var limits = Assert.IsAssignableFrom<Features.Limits.Contracts.LimitsResponseDto>(
            BodyOf(await Run($"limit list --budget-id {budget.BusinessId}")));

        Assert.Contains(limits.Limits, l => l.CategoryId == category.BusinessId && l.Limit == 300m);
    }

    [Fact]
    public async Task StandingOrder_create_pojawia_sie_na_liscie()
    {
        var budget = await SeedBudgetAsync();

        await Run(
            $"standing-order create --budget-id {budget.BusinessId} --name \"Czynsz CLI\" "
            + "--expected-amount 2000 --rhythm Monthly --rules-json "
            + "'[{\"titlePattern\":\"czynsz\",\"amountFrom\":1900,\"amountTo\":2100}]'");

        var list = Assert.IsAssignableFrom<Features.StandingOrders.Contracts.StandingOrdersResponseDto>(
            BodyOf(await Run($"standing-order list --budget-id {budget.BusinessId}")));

        Assert.Contains(list.Orders, o => o.Name == "Czynsz CLI");
    }

    [Fact]
    public async Task EpisodicOrder_create_pojawia_sie_jako_zaplanowane()
    {
        var budget = await SeedBudgetAsync();
        var category = await SeedCategoryAsync();

        await Run(
            $"episodic-order create --budget-id {budget.BusinessId} --name \"Laptop CLI\" "
            + $"--category-id {category.BusinessId} --amount 4000 --due-month 2027-03-01");

        var list = Assert.IsAssignableFrom<Features.EpisodicOrders.Contracts.EpisodicOrdersResponseDto>(
            BodyOf(await Run($"episodic-order list --budget-id {budget.BusinessId}")));

        Assert.Contains(list.Planned, o => o.Name == "Laptop CLI");
    }

    [Fact]
    public async Task Savings_set_goal_i_show_widza_sie_nawzajem()
    {
        var budget = await SeedBudgetWithLinkedSavingsAsync();

        await Run($"savings set-goal --budget-id {budget.BusinessId} --amount 500");

        var savings = Assert.IsAssignableFrom<Features.Savings.Contracts.SavingsResponseDto>(
            BodyOf(await Run($"savings show --budget-id {budget.BusinessId}")));

        Assert.NotNull(savings.Goal);
        Assert.Equal(500m, savings.Goal!.Amount);
    }

    [Fact]
    public async Task Reservation_create_pojawia_sie_na_liscie()
    {
        var budget = await SeedBudgetAsync();

        await Run($"reservation create --budget-id {budget.BusinessId} --name \"Wakacje CLI\" --amount 3000");

        var reservations = Assert.IsAssignableFrom<Features.Savings.Contracts.SavingsReservationsResponseDto>(
            BodyOf(await Run($"reservation list --budget-id {budget.BusinessId}")));

        Assert.Contains(reservations.Reservations, r => r.Name == "Wakacje CLI");
    }

    [Fact]
    public async Task Rule_create_pojawia_sie_na_liscie()
    {
        var category = await SeedCategoryAsync();

        await Run(
            $"rule create --category-id {category.BusinessId} --priority 5 --pattern \"testowy-sprzedawca-cli\"");

        var rules = Assert.IsAssignableFrom<IReadOnlyList<Features.Categorization.Contracts.CategoryRuleResponseDto>>(
            BodyOf(await Run("rule list")));

        Assert.Contains(rules, r => r.Pattern == "testowy-sprzedawca-cli");
    }
}
