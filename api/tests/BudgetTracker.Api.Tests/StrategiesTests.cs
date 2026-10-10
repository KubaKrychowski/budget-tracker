using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Strategies.Commands;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Queries;
using BudgetTracker.Api.Features.Strategies.Services;
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
/// Strategie: zakładanie z szablonu, zapis i odczyt całego grafu przez <c>jsonb</c>, walidacja strukturalna, lista,
/// usuwanie oraz cykl życia budżetu (reset zostawia, usunięcie zabiera, przywrócenie oddaje, purge kasuje).
///
/// Kwoty i nazwy są zmyślone. Wymaga `docker compose up -d db`.
/// </summary>
public sealed class StrategiesTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_strategies_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś” to 3 października 2026.</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

    private readonly Guid _user = Guid.CreateVersion7();

    private static readonly DateOnly October = new(2026, 10, 1);

    private AppDbContext _db = null!;
    private Budget _budget = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _budget = new Budget("Domowy", new DateOnly(2026, 1, 1), 0m, _clock.GetUtcNow());
        _db.Add(_budget);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private StrategiesBudgetScope Scope() => new(_db, _clock);

    private CreateStrategyCommandHandler Create() => new(_db, Scope(), new FakeCurrentUserAccessor(_user), Localizer());

    private SaveStrategyCommandHandler Save() => new(_db, _clock);

    private DuplicateStrategyCommandHandler Duplicate() => new(_db, Scope(), new FakeCurrentUserAccessor(_user));

    private GetStrategyQueryHandler Get() => new(_db);

    private ListStrategiesQueryHandler List() => new(_db, Scope());

    private static IStringLocalizer<SharedResource> Localizer() =>
        new StringLocalizer<SharedResource>(
            new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    private static StrategyNodeRequestDto NodeDto(
        string id, StrategyNodeType type, DateOnly? month = null, decimal? amount = null, decimal? rate = null,
        decimal? installment = null, OverpaymentMode? mode = null, StrategyConditionMetric? metric = null,
        StrategyConditionComparison? comparison = null, decimal? threshold = null, string? title = null, double x = 10, double y = 20) =>
        new(id, type, title, x, y, month, amount, rate, installment, mode, metric, comparison, threshold);

    private static StrategyEdgeRequestDto EdgeDto(string from, string to, StrategyEdgeLabel label = StrategyEdgeLabel.None) =>
        new($"{from}-{to}", from, to, label);

    private static SaveStrategyRequestDto Request(
        IReadOnlyList<StrategyNodeRequestDto>? nodes = null, IReadOnlyList<StrategyEdgeRequestDto>? edges = null,
        string name = "Plan na rok", DateOnly? month = null, decimal cash = 1_000m, int horizon = 24,
        IReadOnlyList<StrategyVariantRequestDto>? variants = null) =>
        new(name, month ?? October, cash, horizon, nodes ?? [], edges ?? [], variants);

    private Task<StrategyResponseDto> NewAsync(string name = "Plan", StrategyTemplate template = StrategyTemplate.Blank) =>
        Create().HandleAsync(new CreateStrategyRequestDto(_budget.BusinessId, name, template), default);

    // ── Zakładanie ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_blank_strategy_starts_empty_in_the_current_month_for_the_current_user()
    {
        var created = await NewAsync();

        Assert.Empty(created.Nodes);
        Assert.Empty(created.Edges);
        Assert.Equal(October, created.StartMonth);
        Assert.Equal(CreateStrategyCommandHandler.DefaultHorizonMonths, created.HorizonMonths);
        Assert.Equal(_budget.BusinessId, created.BudgetId);
        Assert.Equal(_user, (await _db.Strategies.SingleAsync()).UserId);
    }

    [Fact]
    public async Task The_loan_template_has_localized_titles_and_simulates_without_problems()
    {
        var created = await NewAsync(template: StrategyTemplate.LoanAndCushion);

        Assert.All(created.Nodes, n =>
        {
            Assert.False(string.IsNullOrWhiteSpace(n.Title));
            Assert.False(n.Title.StartsWith("Strategy_Template_", StringComparison.Ordinal), "klucz zasobu zamiast tekstu");
        });
        Assert.Empty(created.Result.Problems);
        Assert.NotNull(created.Result.LoanPaidOffIn);
        Assert.NotNull(created.Result.CushionReachedIn);
        Assert.Equal(CreateStrategyCommandHandler.DefaultHorizonMonths, created.Result.Months.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_strategy_needs_a_name(string name)
    {
        await Assert.ThrowsAsync<StrategyNameRequiredException>(() => NewAsync(name));
    }

    [Fact]
    public async Task A_strategy_for_an_unknown_budget_is_a_404_not_a_silent_fallback()
    {
        await Assert.ThrowsAnyAsync<Exception>(() =>
            Create().HandleAsync(new CreateStrategyRequestDto(Guid.NewGuid(), "Plan", StrategyTemplate.Blank), default));
    }

    // ── Zapis i odczyt ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_round_trips_every_node_parameter_through_jsonb()
    {
        var created = await NewAsync();
        var nodes = new[]
        {
            NodeDto("loan", StrategyNodeType.Loan, October, 18_400m, 12.5m, 880m, title: "Kredyt", x: 12.5, y: -40),
            NodeDto("pay", StrategyNodeType.Overpay, amount: 7_000m, mode: OverpaymentMode.ShortenPeriod),
            NodeDto("check", StrategyNodeType.Condition, metric: StrategyConditionMetric.CashMinusDebt,
                comparison: StrategyConditionComparison.AtMost, threshold: 9_000.5m),
            NodeDto("wait", StrategyNodeType.Wait),
        };
        var edges = new[]
        {
            EdgeDto("check", "wait", StrategyEdgeLabel.No),
            EdgeDto("wait", "check"),
        };

        await Save().HandleAsync(created.Id, Request(nodes, edges, cash: 4_600.5m, horizon: 36), default);
        _db.ChangeTracker.Clear();
        var loaded = await Get().HandleAsync(created.Id, default);

        Assert.Equal((4_600.5m, 36), (loaded.StartCash, loaded.HorizonMonths));
        var loan = loaded.Nodes.Single(n => n.Id == "loan");
        Assert.Equal(("Kredyt", StrategyNodeType.Loan, October, 18_400m, 12.5m, 880m), (loan.Title, loan.Type, loan.Month, loan.Amount, loan.Rate, loan.Installment));
        Assert.Equal((12.5, -40d), (loan.X, loan.Y));
        Assert.Equal(OverpaymentMode.ShortenPeriod, loaded.Nodes.Single(n => n.Id == "pay").Mode);
        var check = loaded.Nodes.Single(n => n.Id == "check");
        Assert.Equal((StrategyConditionMetric.CashMinusDebt, StrategyConditionComparison.AtMost, 9_000.5m), (check.Metric, check.Comparison, check.Threshold));
        Assert.Equal(StrategyEdgeLabel.No, loaded.Edges.Single(e => e.Id == "check-wait").Label);
        Assert.Equal(2, loaded.Edges.Count);
    }

    [Fact]
    public async Task Saving_round_trips_the_category_and_standing_order_references_through_jsonb()
    {
        var created = await NewAsync();
        var category = Guid.NewGuid();
        var order = Guid.NewGuid();
        var nodes = new[]
        {
            NodeDto("limit", StrategyNodeType.SetLimit, amount: 1_500m, threshold: 85m) with { CategoryId = category },
            NodeDto("end", StrategyNodeType.EndStandingOrder, October) with { StandingOrderId = order },
            NodeDto("plain", StrategyNodeType.Surplus, amount: 100m),
        };

        await Save().HandleAsync(created.Id, Request(nodes), default);
        _db.ChangeTracker.Clear();
        var loaded = await Get().HandleAsync(created.Id, default);

        Assert.Equal((category, null), (loaded.Nodes.Single(n => n.Id == "limit").CategoryId, (Guid?)null));
        Assert.Equal(order, loaded.Nodes.Single(n => n.Id == "end").StandingOrderId);
        Assert.Null(loaded.Nodes.Single(n => n.Id == "plain").CategoryId);
    }

    [Fact]
    public async Task Saving_trims_the_name_and_snaps_months_to_the_first_day()
    {
        var created = await NewAsync();
        var nodes = new[] { NodeDto("in", StrategyNodeType.Income, new DateOnly(2027, 5, 17), 6_000m) };

        await Save().HandleAsync(created.Id, Request(nodes, name: "  Kredyt i poduszka  ", month: new DateOnly(2026, 11, 28)), default);
        _db.ChangeTracker.Clear();
        var loaded = await Get().HandleAsync(created.Id, default);

        Assert.Equal("Kredyt i poduszka", loaded.Name);
        Assert.Equal(new DateOnly(2026, 11, 1), loaded.StartMonth);
        Assert.Equal(new DateOnly(2027, 5, 1), loaded.Nodes.Single().Month);
    }

    [Fact]
    public async Task A_draft_with_problems_is_saved_and_the_problems_come_back_with_the_result()
    {
        var created = await NewAsync();
        var nodes = new[]
        {
            NodeDto("raise", StrategyNodeType.IncreaseSurplus, amount: 400m),
            NodeDto("in", StrategyNodeType.Income, October, amount: null),
        };

        var saved = await Save().HandleAsync(created.Id, Request(nodes), default);

        Assert.Contains(saved.Result.Problems, p => p is { NodeId: "raise", Kind: StrategyProblemKind.NoIncomingEdge });
        Assert.Contains(saved.Result.Problems, p => p is { NodeId: "in", Kind: StrategyProblemKind.MissingParameter });
        Assert.Equal(2, (await Get().HandleAsync(created.Id, default)).Nodes.Count);
    }

    [Fact]
    public async Task Saving_returns_the_simulation_of_the_saved_graph()
    {
        var created = await NewAsync();
        var nodes = new[] { NodeDto("s", StrategyNodeType.Surplus, amount: 700m) };

        var saved = await Save().HandleAsync(created.Id, Request(nodes, cash: 1_000m, horizon: 6), default);

        Assert.Equal(6, saved.Result.Months.Count);
        Assert.Equal(5_200m, saved.Result.FinalCash);
    }

    [Fact]
    public async Task Saving_moves_the_last_change_timestamp()
    {
        var created = await NewAsync();
        _clock.Advance(TimeSpan.FromHours(5));

        var saved = await Save().HandleAsync(created.Id, Request(), default);

        Assert.Equal(_clock.GetUtcNow(), saved.UpdatedAt);
        Assert.True(saved.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Saving_an_unknown_strategy_is_a_404()
    {
        await Assert.ThrowsAsync<StrategyNotFoundException>(() => Save().HandleAsync(Guid.NewGuid(), Request(), default));
    }

    [Fact]
    public async Task Reading_an_unknown_strategy_is_a_404()
    {
        await Assert.ThrowsAsync<StrategyNotFoundException>(() => Get().HandleAsync(Guid.NewGuid(), default));
    }

    // ── Walidacja strukturalna ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_node_ids_are_rejected()
    {
        var created = await NewAsync();
        var nodes = new[] { NodeDto("a", StrategyNodeType.Surplus, amount: 1m), NodeDto("a", StrategyNodeType.Surplus, amount: 2m) };

        await Assert.ThrowsAsync<StrategyGraphInvalidException>(() => Save().HandleAsync(created.Id, Request(nodes), default));
    }

    [Fact]
    public async Task A_connection_to_a_missing_node_is_rejected()
    {
        var created = await NewAsync();
        var nodes = new[] { NodeDto("a", StrategyNodeType.Trigger, October) };

        await Assert.ThrowsAsync<StrategyGraphInvalidException>(
            () => Save().HandleAsync(created.Id, Request(nodes, [EdgeDto("a", "ghost")]), default));
    }

    [Fact]
    public async Task A_node_connected_to_itself_is_rejected()
    {
        var created = await NewAsync();
        var nodes = new[] { NodeDto("a", StrategyNodeType.Wait) };

        await Assert.ThrowsAsync<StrategyGraphInvalidException>(
            () => Save().HandleAsync(created.Id, Request(nodes, [EdgeDto("a", "a")]), default));
    }

    [Fact]
    public async Task A_yes_no_label_outside_a_condition_is_rejected()
    {
        var created = await NewAsync();
        var nodes = new[] { NodeDto("a", StrategyNodeType.Trigger, October), NodeDto("b", StrategyNodeType.End) };

        await Assert.ThrowsAsync<StrategyGraphInvalidException>(
            () => Save().HandleAsync(created.Id, Request(nodes, [EdgeDto("a", "b", StrategyEdgeLabel.Yes)]), default));
    }

    [Fact]
    public async Task A_horizon_outside_the_range_is_rejected()
    {
        var created = await NewAsync();

        await Assert.ThrowsAsync<StrategyHorizonInvalidException>(() => Save().HandleAsync(created.Id, Request(horizon: 3), default));
        await Assert.ThrowsAsync<StrategyHorizonInvalidException>(() => Save().HandleAsync(created.Id, Request(horizon: 61), default));
    }

    [Fact]
    public async Task A_rejected_save_leaves_the_stored_strategy_untouched()
    {
        var created = await NewAsync(template: StrategyTemplate.LoanAndCushion);
        var nodesBefore = created.Nodes.Count;

        await Assert.ThrowsAsync<StrategyGraphInvalidException>(() => Save().HandleAsync(
            created.Id, Request([NodeDto("a", StrategyNodeType.Wait), NodeDto("a", StrategyNodeType.Wait)]), default));

        _db.ChangeTracker.Clear();
        Assert.Equal(nodesBefore, (await Get().HandleAsync(created.Id, default)).Nodes.Count);
    }

    // ── Podgląd symulacji ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Simulating_an_unsaved_graph_writes_nothing()
    {
        var result = await new SimulateStrategyQueryHandler().HandleAsync(
            Request([NodeDto("s", StrategyNodeType.Surplus, amount: 700m)], horizon: 6), default);

        Assert.Equal(6, result.Months.Count);
        Assert.Empty(await _db.Strategies.ToListAsync());
    }

    [Fact]
    public async Task Simulating_runs_the_same_structural_validation_as_saving()
    {
        await Assert.ThrowsAsync<StrategyGraphInvalidException>(() => new SimulateStrategyQueryHandler().HandleAsync(
            Request([NodeDto("a", StrategyNodeType.Wait)], [EdgeDto("a", "ghost")]), default));
    }

    // ── Lista i usuwanie ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_is_newest_change_first_and_counts_events_and_actions()
    {
        var older = await NewAsync("Starsza");
        _clock.Advance(TimeSpan.FromDays(1));
        var newer = await NewAsync("Nowsza", StrategyTemplate.LoanAndCushion);
        _clock.Advance(TimeSpan.FromDays(1));
        await Save().HandleAsync(older.Id, Request(
            [NodeDto("e", StrategyNodeType.Trigger, October), NodeDto("g", StrategyNodeType.SetSavingsGoal, amount: 100m)],
            [EdgeDto("e", "g")], name: "Starsza"), default);

        var list = await List().HandleAsync(_budget.BusinessId, default);

        Assert.Equal(["Starsza", "Nowsza"], list.Strategies.Select(s => s.Name));
        Assert.Equal((1, 1), (list.Strategies[0].EventCount, list.Strategies[0].ActionCount));
        Assert.Equal(_budget.BusinessId, list.SelectedBudgetId);
        Assert.Equal(newer.Nodes.Count(n => n.Type is StrategyNodeType.Trigger or StrategyNodeType.Income or StrategyNodeType.Expense), list.Strategies[1].EventCount);
    }

    [Fact]
    public async Task The_list_shows_only_the_strategies_of_the_chosen_budget()
    {
        var other = new Budget("Wakacje", new DateOnly(2026, 6, 1), 0m, _clock.GetUtcNow());
        _db.Add(other);
        await _db.SaveChangesAsync();
        await NewAsync("W domowym");
        await Create().HandleAsync(new CreateStrategyRequestDto(other.BusinessId, "Na wakacje", StrategyTemplate.Blank), default);

        var home = await List().HandleAsync(_budget.BusinessId, default);
        var holiday = await List().HandleAsync(other.BusinessId, default);

        Assert.Equal("W domowym", Assert.Single(home.Strategies).Name);
        Assert.Equal("Na wakacje", Assert.Single(holiday.Strategies).Name);
    }

    [Fact]
    public async Task Deleting_hides_the_strategy_and_a_second_delete_is_a_404()
    {
        var created = await NewAsync();

        await new DeleteStrategyCommandHandler(_db).HandleAsync(created.Id, default);

        Assert.Empty((await List().HandleAsync(_budget.BusinessId, default)).Strategies);
        Assert.Single(await _db.Strategies.IgnoreQueryFilters().ToListAsync());
        await Assert.ThrowsAsync<StrategyNotFoundException>(() => new DeleteStrategyCommandHandler(_db).HandleAsync(created.Id, default));
    }

    // ── Cykl życia budżetu ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resetting_the_budget_leaves_the_strategy_alone()
    {
        await NewAsync(template: StrategyTemplate.LoanAndCushion);

        await new BudgetChildren(_db).SoftDeleteAsync(_budget, _clock.GetUtcNow(), default);

        Assert.Single(await _db.Strategies.ToListAsync());
    }

    [Fact]
    public async Task Deleting_the_budget_takes_the_strategy_restoring_gives_it_back_and_purge_removes_it()
    {
        await NewAsync(template: StrategyTemplate.LoanAndCushion);
        var children = new BudgetChildren(_db);
        var at = _clock.GetUtcNow();

        await children.SoftDeleteSavingsAsync(_budget, at, default);
        Assert.Empty(await _db.Strategies.ToListAsync());

        await children.RestoreAsync(_budget, at, default);
        Assert.Single(await _db.Strategies.ToListAsync());

        await children.SoftDeleteSavingsAsync(_budget, at, default);
        _budget.MarkDeleted(at);
        await _db.SaveChangesAsync();
        await new BudgetPurger(_db).PurgeAsync(null, default);
        Assert.Empty(await _db.Strategies.IgnoreQueryFilters().ToListAsync());
    }

    // ── Warianty ─────────────────────────────────────────────────────────────────────────

    private static readonly StrategyNodeRequestDto[] RaiseChain =
    [
        NodeDto("raise", StrategyNodeType.Trigger, October),
        NodeDto("more", StrategyNodeType.IncreaseSurplus, amount: 400m),
        NodeDto("s", StrategyNodeType.Surplus, amount: 700m),
    ];

    [Fact]
    public async Task Variants_are_saved_with_their_own_result_while_the_base_result_stays_whole()
    {
        var created = await NewAsync();

        var saved = await Save().HandleAsync(
            created.Id,
            Request(
                RaiseChain, [EdgeDto("raise", "more")], horizon: 6,
                variants: [new StrategyVariantRequestDto("v1", "  Bez podwyżki ", ["raise"])]),
            default);
        var reloaded = await Get().HandleAsync(created.Id, default);

        var variant = Assert.Single(saved.Variants);
        Assert.Equal("Bez podwyżki", variant.Name);
        Assert.Equal(1_000m + 6 * 1_100m, saved.Result.FinalCash);
        Assert.Equal(1_000m + 6 * 700m, variant.Result.FinalCash);
        Assert.Equal(["raise"], Assert.Single(reloaded.Variants).DisabledNodeIds);
        Assert.Equal(variant.Result.FinalCash, reloaded.Variants[0].Result.FinalCash);
    }

    [Fact]
    public async Task A_request_without_variants_clears_them_and_old_rows_read_as_having_none()
    {
        var created = await NewAsync();
        await Save().HandleAsync(
            created.Id,
            Request(RaiseChain, [EdgeDto("raise", "more")], variants: [new StrategyVariantRequestDto("v1", "A", ["raise"])]),
            default);

        var cleared = await Save().HandleAsync(created.Id, Request(RaiseChain, [EdgeDto("raise", "more")]), default);

        Assert.Empty(created.Variants);
        Assert.Empty(cleared.Variants);
    }

    [Fact]
    public async Task A_variant_cannot_disable_a_tile_that_is_not_on_the_board()
    {
        var created = await NewAsync();

        await Assert.ThrowsAsync<StrategyVariantInvalidException>(() => Save().HandleAsync(
            created.Id, Request(RaiseChain, variants: [new StrategyVariantRequestDto("v1", "A", ["ghost"])]), default));
    }

    [Fact]
    public async Task Variant_names_and_ids_must_be_filled_and_unique_and_there_are_at_most_ten()
    {
        var created = await NewAsync();
        Task Save(params StrategyVariantRequestDto[] variants) =>
            this.Save().HandleAsync(created.Id, Request(RaiseChain, variants: variants), default);

        await Assert.ThrowsAsync<StrategyVariantInvalidException>(() => Save(new StrategyVariantRequestDto("v1", "  ", [])));
        await Assert.ThrowsAsync<StrategyVariantInvalidException>(
            () => Save(new StrategyVariantRequestDto("v1", new string('x', 61), [])));
        await Assert.ThrowsAsync<StrategyVariantInvalidException>(
            () => Save(new StrategyVariantRequestDto("v1", "A", []), new StrategyVariantRequestDto("v1", "B", [])));
        await Assert.ThrowsAsync<StrategyVariantInvalidException>(
            () => Save(new StrategyVariantRequestDto("v1", "Bez", []), new StrategyVariantRequestDto("v2", "BEZ", [])));
        await Assert.ThrowsAsync<StrategyVariantInvalidException>(() => Save([.. Enumerable.Range(0, 11)
            .Select(i => new StrategyVariantRequestDto($"v{i}", $"Wariant {i}", []))]));
        await Save([.. Enumerable.Range(0, 10).Select(i => new StrategyVariantRequestDto($"v{i}", $"Wariant {i}", []))]);
    }

    [Fact]
    public async Task Simulating_variants_returns_the_base_and_every_variant_without_saving_anything()
    {
        var created = await NewAsync();
        var request = Request(
            RaiseChain, [EdgeDto("raise", "more")], horizon: 6,
            variants: [new StrategyVariantRequestDto("v1", "Bez podwyżki", ["raise"])]);

        var result = await new SimulateStrategyVariantsQueryHandler().HandleAsync(request, default);

        Assert.Equal(1_000m + 6 * 1_100m, result.Base.FinalCash);
        var variant = Assert.Single(result.Variants);
        Assert.Equal("v1", variant.Id);
        Assert.Equal(1_000m + 6 * 700m, variant.Result.FinalCash);
        Assert.Empty((await Get().HandleAsync(created.Id, default)).Variants);
    }

    // ── Duplikowanie ─────────────────────────────────────────────────────────────────────

    private async Task<StrategyResponseDto> WithVariantAsync()
    {
        var created = await NewAsync("Oryginał");
        return await Save().HandleAsync(
            created.Id,
            Request(
                RaiseChain, [EdgeDto("raise", "more")], name: "Oryginał", cash: 2_500m, horizon: 12,
                variants: [new StrategyVariantRequestDto("v1", "Bez podwyżki", ["raise"])]),
            default);
    }

    [Fact]
    public async Task A_copy_has_the_same_graph_parameters_and_variants_under_the_new_name_in_the_same_budget()
    {
        var original = await WithVariantAsync();

        var copy = await Duplicate().HandleAsync(original.Id, new DuplicateStrategyRequestDto("  Kopia planu "), default);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Kopia planu", copy.Name);
        Assert.Equal(original.BudgetId, copy.BudgetId);
        Assert.Equal((original.StartMonth, original.StartCash, original.HorizonMonths), (copy.StartMonth, copy.StartCash, copy.HorizonMonths));
        Assert.Equal(original.Nodes.Select(n => n.Id), copy.Nodes.Select(n => n.Id));
        Assert.Equal(original.Edges.Select(e => e.Id), copy.Edges.Select(e => e.Id));
        Assert.Equal(original.Result.FinalCash, copy.Result.FinalCash);
        var variant = Assert.Single(copy.Variants);
        Assert.Equal(("v1", "Bez podwyżki"), (variant.Id, variant.Name));
        Assert.Equal(original.Variants[0].Result.FinalCash, variant.Result.FinalCash);
        Assert.Equal(_user, (await _db.Strategies.SingleAsync(s => s.BusinessId == copy.Id)).UserId);
    }

    [Fact]
    public async Task Changing_the_copy_leaves_the_original_alone()
    {
        // Łapie kopię współdzielącą listy z oryginałem: zapis kopii zmieniałby też oryginał.
        var original = await WithVariantAsync();
        var copy = await Duplicate().HandleAsync(original.Id, new DuplicateStrategyRequestDto("Kopia"), default);

        await Save().HandleAsync(copy.Id, Request([RaiseChain[2]], name: "Kopia", variants: []), default);

        var untouched = await Get().HandleAsync(original.Id, default);
        Assert.Equal(3, untouched.Nodes.Count);
        Assert.Single(untouched.Variants);
        Assert.Single((await Get().HandleAsync(copy.Id, default)).Nodes);
    }

    [Fact]
    public async Task A_copy_without_variants_keeps_the_graph_and_has_only_the_base_variant()
    {
        var original = await WithVariantAsync();

        var copy = await Duplicate().HandleAsync(original.Id, new DuplicateStrategyRequestDto("Bez wariantów", CopyVariants: false), default);

        Assert.Equal(3, copy.Nodes.Count);
        Assert.Empty(copy.Variants);
        Assert.Single((await Get().HandleAsync(original.Id, default)).Variants);
    }

    [Fact]
    public async Task Duplicating_needs_a_name_and_an_existing_strategy()
    {
        var original = await NewAsync();

        await Assert.ThrowsAsync<StrategyNameRequiredException>(
            () => Duplicate().HandleAsync(original.Id, new DuplicateStrategyRequestDto("   "), default));
        await Assert.ThrowsAsync<StrategyNameRequiredException>(
            () => Duplicate().HandleAsync(original.Id, new DuplicateStrategyRequestDto(new string('x', 101)), default));
        await Assert.ThrowsAsync<StrategyNotFoundException>(
            () => Duplicate().HandleAsync(Guid.NewGuid(), new DuplicateStrategyRequestDto("Kopia"), default));
    }

    [Fact]
    public async Task The_list_tells_how_many_variants_each_strategy_has()
    {
        await WithVariantAsync();
        await NewAsync("Pusta");

        var list = await List().HandleAsync(_budget.BusinessId, default);

        var counts = list.Strategies.ToDictionary(r => r.Name, r => r.VariantCount);
        Assert.Equal(1, counts["Oryginał"]);
        Assert.Equal(0, counts["Pusta"]);
    }
}
