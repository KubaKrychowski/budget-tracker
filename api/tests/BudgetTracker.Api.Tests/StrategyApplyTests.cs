using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.EpisodicOrders.Commands;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Features.Limits.Commands;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Features.Savings.Commands;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Features.StandingOrders.Commands;
using BudgetTracker.Api.Features.Strategies.Commands;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Queries;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// „Zastosuj w budżecie”: podgląd statusów akcji (nowa / zmiana / już jest / czeka / niekompletna) i samo zakładanie
/// celu, rezerwacji, limitu, zakończenia zlecenia stałego i wydatku jednorazowego tymi samymi handlerami co ekrany.
///
/// Kwoty i nazwy są zmyślone. Wymaga `docker compose up -d db`.
/// </summary>
public sealed class StrategyApplyTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_strategy_apply_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś” to 3 października 2026.</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly October = new(2026, 10, 1);

    private AppDbContext _db = null!;
    private Budget _budget = null!;
    private Category _food = null!;
    private StandingOrder _order = null!;

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
        _db.Categories.Add(_food);
        _budget = new Budget("Domowy", new DateOnly(2026, 1, 1), 0m, _clock.GetUtcNow());
        _db.Budgets.Add(_budget);
        await _db.SaveChangesAsync();

        _order = new StandingOrder(_budget.BusinessId, "Abonament", 80m, StandingOrderRhythm.Monthly, null, _clock.GetUtcNow());
        _db.StandingOrders.Add(_order);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private GetStrategyApplyPreviewQueryHandler Preview() =>
        new(_db, new StrategyApplyPlanner(_db, new LimitCategories(_db)), new StrategiesBudgetScope(_db, _clock));

    private ApplyStrategyCommandHandler Apply()
    {
        var user = new FakeCurrentUserAccessor(Guid.NewGuid());
        var savingsScope = new SavingsBudgetScope(_db, _clock);
        return new ApplyStrategyCommandHandler(
            _db,
            new StrategyApplyPlanner(_db, new LimitCategories(_db)),
            new StrategiesBudgetScope(_db, _clock),
            new SetSavingsGoalCommandHandler(_db, savingsScope, _clock, user),
            new CreateSavingsReservationCommandHandler(_db, savingsScope, _clock, user),
            new SetLimitCommandHandler(_db, new LimitsBudgetScope(_db, _clock), new LimitCategories(_db), user),
            new EndStandingOrderCommandHandler(_db),
            new CreateEpisodicOrderCommandHandler(
                _db, new EpisodicOrdersBudgetScope(_db, _clock), new EpisodicOrderRequestValidator(_db),
                new EpisodicOrderTransactions(_db), user));
    }

    private static StrategyNode Trigger(DateOnly month) =>
        new("t", StrategyNodeType.Trigger, "Zdarzenie", 0, 0, month, null, null, null, null, null, null, null);

    private static StrategyNode Action(
        string id, StrategyNodeType type, decimal? amount = null, string title = "", DateOnly? month = null,
        Guid? categoryId = null, Guid? orderId = null, decimal? threshold = null) =>
        new(id, type, title, 0, 0, month, amount, null, null, null, null, null, threshold, categoryId, orderId);

    /// <summary>Strategia, w której zdarzenie w <paramref name="eventMonth"/> uruchamia wszystkie podane akcje.</summary>
    private async Task<Strategy> StrategyAsync(DateOnly eventMonth, params StrategyNode[] actions)
    {
        var nodes = new List<StrategyNode> { Trigger(eventMonth) };
        nodes.AddRange(actions);
        var edges = actions.Select(a => new StrategyEdge($"t-{a.Id}", "t", a.Id, StrategyEdgeLabel.None)).ToList();
        var strategy = new Strategy(_budget.BusinessId, "Plan", October, 1_000m, 24, _clock.GetUtcNow());
        strategy.Replace("Plan", October, 1_000m, 24, nodes, edges, _clock.GetUtcNow());
        _db.Strategies.Add(strategy);
        await _db.SaveChangesAsync();
        return strategy;
    }

    private async Task<StrategyApplyItemResponseDto> ItemAsync(Strategy strategy, string nodeId) =>
        (await Preview().HandleAsync(strategy.BusinessId, default)).Items.Single(i => i.NodeId == nodeId);

    // ── Podgląd ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_lists_only_budget_actions_with_the_budget_name()
    {
        var strategy = await StrategyAsync(
            October,
            Action("goal", StrategyNodeType.SetSavingsGoal, 700m),
            Action("surplus", StrategyNodeType.IncreaseSurplus, 100m));

        var preview = await Preview().HandleAsync(strategy.BusinessId, default);

        Assert.Equal("Domowy", preview.BudgetName);
        Assert.Equal(["goal"], preview.Items.Select(i => i.NodeId));
    }

    [Fact]
    public async Task An_action_whose_month_has_not_come_yet_waits()
    {
        var strategy = await StrategyAsync(
            new DateOnly(2027, 3, 1), Action("goal", StrategyNodeType.SetSavingsGoal, 700m));

        Assert.Equal(StrategyApplyStatus.Waiting, (await ItemAsync(strategy, "goal")).Status);
    }

    [Fact]
    public async Task An_action_with_a_missing_parameter_is_incomplete()
    {
        var strategy = await StrategyAsync(
            October,
            Action("limit", StrategyNodeType.SetLimit, 900m),
            Action("reservation", StrategyNodeType.CreateReservation, 500m));

        Assert.Equal(StrategyApplyStatus.Incomplete, (await ItemAsync(strategy, "limit")).Status);
        Assert.Equal(StrategyApplyStatus.Incomplete, (await ItemAsync(strategy, "reservation")).Status);
    }

    [Fact]
    public async Task A_goal_is_new_then_a_change_for_another_amount_then_exists_for_the_same_one()
    {
        var strategy = await StrategyAsync(October, Action("goal", StrategyNodeType.SetSavingsGoal, 700m));
        Assert.Equal(StrategyApplyStatus.New, (await ItemAsync(strategy, "goal")).Status);

        _db.SavingsGoals.Add(new SavingsGoal(_budget.BusinessId, 400m, October, _clock.GetUtcNow(), Guid.NewGuid()));
        await _db.SaveChangesAsync();
        var change = await ItemAsync(strategy, "goal");
        Assert.Equal(StrategyApplyStatus.Change, change.Status);
        Assert.Equal(400m, change.CurrentAmount);

        await Apply().HandleAsync(strategy.BusinessId, new ApplyStrategyRequestDto(["goal"]), default);
        Assert.Equal(StrategyApplyStatus.Exists, (await ItemAsync(strategy, "goal")).Status);
    }

    // ── Zastosowanie ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Applying_creates_every_kind_of_object_in_the_strategys_budget()
    {
        var strategy = await StrategyAsync(
            October,
            Action("goal", StrategyNodeType.SetSavingsGoal, 700m),
            Action("reservation", StrategyNodeType.CreateReservation, 2_200m, "Ubezpieczenie", new DateOnly(2026, 12, 1)),
            Action("limit", StrategyNodeType.SetLimit, 1_500m, categoryId: _food.BusinessId, threshold: 90m),
            Action("end", StrategyNodeType.EndStandingOrder, month: new DateOnly(2027, 7, 15), orderId: _order.BusinessId),
            Action("expense", StrategyNodeType.CreateEpisodicOrder, 3_000m, "Remont", categoryId: _food.BusinessId));

        var result = await Apply().HandleAsync(
            strategy.BusinessId, new ApplyStrategyRequestDto(["goal", "reservation", "limit", "end", "expense"]), default);

        Assert.Equal(5, result.Applied.Count);
        Assert.Empty(result.Skipped);
        Assert.Equal(700m, (await _db.SavingsGoals.SingleAsync()).Amount);
        var reservation = await _db.SavingsReservations.SingleAsync();
        Assert.Equal(("Ubezpieczenie", 2_200m, new DateOnly(2026, 12, 1)), (reservation.Name, reservation.Amount, reservation.DueMonth));
        var limit = await _db.BudgetItems.SingleAsync();
        Assert.Equal((1_500m, 90, October), (limit.Limit, limit.WarningThreshold, limit.ValidFrom));
        Assert.Equal(new DateOnly(2027, 7, 1), (await _db.StandingOrders.SingleAsync()).EndMonth);
        var expense = await _db.EpisodicOrders.SingleAsync();
        Assert.Equal(("Remont", 3_000m, October), (expense.Name, expense.PlannedAmount, expense.DueMonth));
        Assert.All(
            [reservation.BudgetBusinessId, limit.BudgetBusinessId, expense.BudgetBusinessId],
            id => Assert.Equal(_budget.BusinessId, id));
    }

    [Fact]
    public async Task Applying_twice_does_not_duplicate_anything()
    {
        var strategy = await StrategyAsync(
            October,
            Action("reservation", StrategyNodeType.CreateReservation, 2_200m, "Ubezpieczenie"),
            Action("expense", StrategyNodeType.CreateEpisodicOrder, 3_000m, "Remont", categoryId: _food.BusinessId));
        var request = new ApplyStrategyRequestDto(["reservation", "expense"]);

        await Apply().HandleAsync(strategy.BusinessId, request, default);
        var second = await Apply().HandleAsync(strategy.BusinessId, request, default);

        Assert.Empty(second.Applied);
        Assert.Equal(2, second.Skipped.Count);
        Assert.Single(await _db.SavingsReservations.ToListAsync());
        Assert.Single(await _db.EpisodicOrders.ToListAsync());
    }

    [Fact]
    public async Task Only_selected_actions_are_applied_and_waiting_ones_are_skipped()
    {
        var strategy = await StrategyAsync(
            October,
            Action("goal", StrategyNodeType.SetSavingsGoal, 700m),
            Action("reservation", StrategyNodeType.CreateReservation, 500m, "Wakacje"));

        var onlyReservation = await Apply().HandleAsync(
            strategy.BusinessId, new ApplyStrategyRequestDto(["reservation"]), default);

        Assert.Equal(["reservation"], onlyReservation.Applied);
        Assert.Empty(await _db.SavingsGoals.ToListAsync());

        var later = await StrategyAsync(new DateOnly(2027, 3, 1), Action("goal", StrategyNodeType.SetSavingsGoal, 700m));
        var waiting = await Apply().HandleAsync(later.BusinessId, new ApplyStrategyRequestDto(["goal", "unknown"]), default);
        Assert.Empty(waiting.Applied);
        Assert.Equal(["goal", "unknown"], waiting.Skipped);
        Assert.Empty(await _db.SavingsGoals.ToListAsync());
    }

    [Fact]
    public async Task Applying_nothing_selected_is_rejected()
    {
        var strategy = await StrategyAsync(October, Action("goal", StrategyNodeType.SetSavingsGoal, 700m));

        await Assert.ThrowsAsync<StrategyApplyNothingSelectedException>(
            () => Apply().HandleAsync(strategy.BusinessId, new ApplyStrategyRequestDto([]), default));
    }

    [Fact]
    public async Task References_list_the_budgets_categories_and_only_standing_orders_that_are_not_ended()
    {
        var ended = new StandingOrder(_budget.BusinessId, "Stary", 10m, StandingOrderRhythm.Monthly, null, _clock.GetUtcNow());
        ended.End(October);
        _db.StandingOrders.Add(ended);
        await _db.SaveChangesAsync();
        var strategy = await StrategyAsync(October);

        var references = await new GetStrategyReferencesQueryHandler(_db, new LimitCategories(_db))
            .HandleAsync(strategy.BusinessId, default);

        Assert.Contains(references.Categories, c => c.Id == _food.BusinessId && c.Name == "Jedzenie");
        Assert.Equal(["Abonament"], references.StandingOrders.Select(o => o.Name));
    }

    [Fact]
    public async Task An_unknown_strategy_is_not_found()
    {
        await Assert.ThrowsAsync<StrategyNotFoundException>(
            () => Preview().HandleAsync(Guid.NewGuid(), default));
    }
}
