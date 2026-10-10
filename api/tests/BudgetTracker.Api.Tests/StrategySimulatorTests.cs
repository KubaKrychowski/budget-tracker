using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Models;
using BudgetTracker.Api.Features.Strategies.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Symulator strategii: gotówka i dług miesiąc po miesiącu, zdarzenia, warunki, „Czekaj”, problemy grafu.
/// Czysta funkcja — bez bazy i Dockera. Kwoty i scenariusz są zmyślone (kształt jak kredyt + premia + poduszka).
/// </summary>
public sealed class StrategySimulatorTests
{
    private static readonly DateOnly October2026 = new(2026, 10, 1);

    private static DateOnly M(int year, int month) => new(year, month, 1);

    private static StrategyNode Node(
        string id, StrategyNodeType type, DateOnly? month = null, decimal? amount = null, decimal? rate = null,
        decimal? installment = null, OverpaymentMode? mode = null, StrategyConditionMetric? metric = null,
        StrategyConditionComparison? comparison = null, decimal? threshold = null) =>
        new(id, type, string.Empty, 0, 0, month, amount, rate, installment, mode, metric, comparison, threshold);

    private static StrategyEdge Edge(string from, string to, StrategyEdgeLabel label = StrategyEdgeLabel.None) =>
        new($"{from}->{to}", from, to, label);

    private static StrategyResult Run(
        IReadOnlyList<StrategyNode> nodes, IReadOnlyList<StrategyEdge>? edges = null, decimal cash = 0m, int months = 24) =>
        StrategySimulator.Run(new StrategyInput(October2026, cash, months, nodes, edges ?? []));

    private static StrategyNode Loan(decimal balance = 18_400m, decimal rate = 12m, decimal installment = 880m) =>
        Node("loan", StrategyNodeType.Loan, amount: balance, rate: rate, installment: installment);

    [Fact]
    public void Monthly_surplus_adds_to_cash_every_month_from_the_start()
    {
        var result = Run([Node("s", StrategyNodeType.Surplus, amount: 700m)], cash: 1_000m, months: 6);

        Assert.Equal(6, result.Months.Count);
        Assert.Equal(M(2026, 10), result.Months[0].Month);
        Assert.Equal(1_700m, result.Months[0].Cash);
        Assert.Equal(5_200m, result.FinalCash);
    }

    [Fact]
    public void Loan_interest_uses_the_actual_days_of_the_month()
    {
        var result = Run([Loan()], months: 6);

        var october = Math.Round(18_400m * 0.12m * 31m / 365m, 2);
        Assert.Equal(october, result.Months[0].Interest);
        Assert.Equal(Math.Round(18_400m + october - 880m, 2), result.Months[0].Debt);

        var november = Math.Round(result.Months[0].Debt * 0.12m * 30m / 365m, 2);
        Assert.Equal(november, result.Months[1].Interest);
    }

    [Fact]
    public void Loan_is_paid_off_by_installments_and_the_month_is_reported()
    {
        var result = Run([Loan(balance: 1_500m, installment: 800m)], months: 6);

        Assert.Equal(0m, result.Months[^1].Debt);
        Assert.Equal(M(2026, 11), result.LoanPaidOffIn);
        Assert.Equal(result.Months.Sum(m => m.Interest), result.TotalInterest);
    }

    [Fact]
    public void One_off_income_and_expense_events_hit_cash_in_their_own_month()
    {
        var result = Run(
        [
            Node("in", StrategyNodeType.Income, M(2026, 12), 6_000m),
            Node("out", StrategyNodeType.Expense, M(2027, 1), 2_200m),
        ], cash: 500m, months: 6);

        Assert.Equal(500m, result.Months[1].Cash);
        Assert.Equal(6_500m, result.Months[2].Cash);
        Assert.Equal(4_300m, result.Months[3].Cash);
    }

    [Fact]
    public void An_event_outside_the_horizon_never_fires()
    {
        var result = Run([Node("in", StrategyNodeType.Income, M(2030, 1), 6_000m)], cash: 100m, months: 12);

        Assert.Equal(100m, result.FinalCash);
        Assert.DoesNotContain(result.Nodes, n => n.NodeId == "in");
    }

    [Fact]
    public void An_action_after_an_event_runs_in_the_event_month_and_changes_the_surplus_from_that_month()
    {
        var result = Run(
        [
            Node("s", StrategyNodeType.Surplus, amount: 700m),
            Node("raise", StrategyNodeType.Trigger, M(2027, 1)),
            Node("plus", StrategyNodeType.IncreaseSurplus, amount: 400m),
        ], [Edge("raise", "plus")], months: 6);

        Assert.Equal(700m, result.Months[2].Cash - result.Months[1].Cash);
        Assert.Equal(1_100m, result.Months[3].Cash - result.Months[2].Cash);
        Assert.Equal(M(2027, 1), result.Nodes.Single(n => n.NodeId == "plus").FiredIn);
    }

    [Fact]
    public void An_overpayment_charges_interest_on_the_lower_balance_in_the_same_month()
    {
        var result = Run(
        [
            Loan(balance: 10_000m),
            Node("bonus", StrategyNodeType.Income, M(2026, 10), 4_000m),
            Node("pay", StrategyNodeType.Overpay, amount: 4_000m, mode: OverpaymentMode.ShortenPeriod),
        ], [Edge("bonus", "pay")], months: 6);

        var interest = Math.Round(6_000m * 0.12m * 31m / 365m, 2);
        Assert.Equal(interest, result.Months[0].Interest);
        Assert.Equal(0m, result.Months[0].Cash);
    }

    [Fact]
    public void Reducing_the_installment_lowers_it_in_proportion_and_shortening_keeps_it()
    {
        StrategyNode[] Graph(OverpaymentMode mode) =>
        [
            Loan(balance: 10_000m, rate: 0m, installment: 1_000m),
            Node("bonus", StrategyNodeType.Income, M(2026, 10), 5_000m),
            Node("pay", StrategyNodeType.Overpay, amount: 5_000m, mode: mode),
        ];
        var edges = new[] { Edge("bonus", "pay") };

        var reduced = Run(Graph(OverpaymentMode.ReduceInstallment), edges, months: 6);
        var shortened = Run(Graph(OverpaymentMode.ShortenPeriod), edges, months: 6);

        Assert.Equal(4_000m, reduced.Months[1].Debt);
        Assert.Equal(3_000m, shortened.Months[1].Debt);
    }

    [Fact]
    public void An_overpayment_is_capped_by_the_cash_on_hand()
    {
        var result = Run(
        [
            Loan(balance: 10_000m, rate: 0m, installment: 1_000m),
            Node("e", StrategyNodeType.Trigger, M(2026, 10)),
            Node("pay", StrategyNodeType.Overpay, amount: 8_000m, mode: OverpaymentMode.ShortenPeriod),
        ], [Edge("e", "pay")], cash: 3_000m, months: 6);

        Assert.Equal(0m, result.Months[0].Cash);
        Assert.Equal(6_000m, result.Months[0].Debt);
    }

    [Fact]
    public void A_met_condition_follows_the_yes_edge_and_pays_off_the_rest_of_the_loan()
    {
        var result = Run(
        [
            Loan(balance: 5_000m, rate: 0m, installment: 100m),
            Node("bonus", StrategyNodeType.Income, M(2026, 12), 20_000m),
            Node("check", StrategyNodeType.Condition, metric: StrategyConditionMetric.CashMinusDebt,
                comparison: StrategyConditionComparison.AtLeast, threshold: 10_000m),
            Node("payoff", StrategyNodeType.PayOffLoan),
        ],
        [Edge("bonus", "check"), Edge("check", "payoff", StrategyEdgeLabel.Yes)], months: 6);

        Assert.Equal(M(2026, 12), result.LoanPaidOffIn);
        Assert.Equal(M(2026, 12), result.Nodes.Single(n => n.NodeId == "check").ConditionMetIn);
        Assert.Equal(0m, result.Months[2].Debt);
        Assert.Equal(15_300m, result.Months[2].Cash);
    }

    [Fact]
    public void An_unmet_condition_waits_a_month_and_checks_again_until_it_passes()
    {
        var result = Run(
        [
            Node("s", StrategyNodeType.Surplus, amount: 1_000m),
            Loan(balance: 3_000m, rate: 0m, installment: 0.01m),
            Node("e", StrategyNodeType.Trigger, M(2026, 10)),
            Node("check", StrategyNodeType.Condition, metric: StrategyConditionMetric.Cash,
                comparison: StrategyConditionComparison.AtLeast, threshold: 5_000m),
            Node("wait", StrategyNodeType.Wait),
            Node("payoff", StrategyNodeType.PayOffLoan),
        ],
        [
            Edge("e", "check"),
            Edge("check", "payoff", StrategyEdgeLabel.Yes),
            Edge("check", "wait", StrategyEdgeLabel.No),
            Edge("wait", "check"),
        ], months: 12);

        Assert.DoesNotContain(result.Problems, p => p.Kind == StrategyProblemKind.Cycle);
        Assert.Equal(M(2026, 10), result.Nodes.Single(n => n.NodeId == "check").FiredIn);
        Assert.Equal(M(2027, 2), result.Nodes.Single(n => n.NodeId == "check").ConditionMetIn);
        Assert.Equal(M(2027, 2), result.LoanPaidOffIn);
    }

    [Fact]
    public void A_node_without_an_incoming_edge_is_reported_and_never_runs()
    {
        var result = Run([Node("s", StrategyNodeType.Surplus, amount: 100m), Node("plus", StrategyNodeType.IncreaseSurplus, amount: 900m)],
            months: 6);

        Assert.Contains(result.Problems, p => p is { NodeId: "plus", Kind: StrategyProblemKind.NoIncomingEdge });
        Assert.Equal(100m, result.Months[1].Cash - result.Months[0].Cash);
        Assert.DoesNotContain(result.Nodes, n => n.NodeId == "plus");
    }

    [Fact]
    public void A_node_missing_a_required_parameter_is_reported_and_skipped()
    {
        var result = Run(
        [
            Node("e", StrategyNodeType.Income, M(2026, 11), amount: null),
            Node("s", StrategyNodeType.Surplus, amount: 100m),
        ], months: 6);

        Assert.Contains(result.Problems, p => p is { NodeId: "e", Kind: StrategyProblemKind.MissingParameter });
        Assert.Equal(600m, result.FinalCash);
    }

    [Fact]
    public void A_one_off_expense_action_subtracts_cash_in_the_month_it_fires()
    {
        var result = Run(
        [
            Node("raise", StrategyNodeType.Trigger, M(2026, 12)),
            BudgetAction(StrategyNodeType.CreateEpisodicOrder, 3_000m, "Remont", categoryId: Guid.NewGuid()) with { Id = "repair" },
            Node("s", StrategyNodeType.Surplus, amount: 1_000m),
        ], [Edge("raise", "repair")], cash: 5_000m, months: 6);

        Assert.Equal(M(2026, 12), result.Nodes.Single(n => n.NodeId == "repair").FiredIn);
        Assert.Equal(5_000m + 3 * 1_000m, result.Months[2].Cash + 3_000m);
        Assert.Equal(5_000m + 6 * 1_000m - 3_000m, result.FinalCash);
    }

    private static bool IsMissingParameter(StrategyNode action) =>
        StrategyGraphAnalyzer.Analyze([Node("t", StrategyNodeType.Trigger, M(2026, 10)), action], [Edge("t", action.Id)])
            .Any(p => p is { NodeId: var id, Kind: StrategyProblemKind.MissingParameter } && id == action.Id);

    private static StrategyNode BudgetAction(
        StrategyNodeType type, decimal? amount = null, string title = "", DateOnly? month = null,
        Guid? categoryId = null, Guid? orderId = null) =>
        new("a", type, title, 0, 0, month, amount, null, null, null, null, null, null, categoryId, orderId);

    [Fact]
    public void A_budget_action_is_complete_only_with_the_parameters_it_needs_to_be_applied()
    {
        var category = Guid.NewGuid();
        var order = Guid.NewGuid();

        Assert.True(IsMissingParameter(BudgetAction(StrategyNodeType.SetSavingsGoal)));
        Assert.False(IsMissingParameter(BudgetAction(StrategyNodeType.SetSavingsGoal, 700m)));

        Assert.True(IsMissingParameter(BudgetAction(StrategyNodeType.CreateReservation, 500m)));
        Assert.False(IsMissingParameter(BudgetAction(StrategyNodeType.CreateReservation, 500m, "Wakacje")));

        Assert.True(IsMissingParameter(BudgetAction(StrategyNodeType.SetLimit, 900m)));
        Assert.False(IsMissingParameter(BudgetAction(StrategyNodeType.SetLimit, 900m, categoryId: category)));

        Assert.True(IsMissingParameter(BudgetAction(StrategyNodeType.EndStandingOrder, orderId: order)));
        Assert.False(IsMissingParameter(BudgetAction(StrategyNodeType.EndStandingOrder, month: M(2027, 7), orderId: order)));

        Assert.True(IsMissingParameter(BudgetAction(StrategyNodeType.CreateEpisodicOrder, 500m, "Remont")));
        Assert.False(IsMissingParameter(BudgetAction(StrategyNodeType.CreateEpisodicOrder, 500m, "Remont", categoryId: category)));
    }

    [Fact]
    public void A_cycle_that_does_not_pass_through_wait_is_reported_and_does_not_hang()
    {
        var result = Run(
        [
            Node("e", StrategyNodeType.Trigger, M(2026, 10)),
            Node("a", StrategyNodeType.IncreaseSurplus, amount: 100m),
            Node("b", StrategyNodeType.IncreaseSurplus, amount: 100m),
        ], [Edge("e", "a"), Edge("a", "b"), Edge("b", "a")], months: 6);

        Assert.Contains(result.Problems, p => p is { NodeId: "a", Kind: StrategyProblemKind.Cycle });
        Assert.Contains(result.Problems, p => p is { NodeId: "b", Kind: StrategyProblemKind.Cycle });
        Assert.Equal(0m, result.FinalCash);
    }

    [Fact]
    public void A_trigger_without_actions_and_a_wait_without_a_successor_are_flagged()
    {
        var result = Run(
        [
            Node("e", StrategyNodeType.Trigger, M(2026, 10)),
            Node("w", StrategyNodeType.Wait),
        ], [Edge("e", "w")], months: 6);

        Assert.Contains(result.Problems, p => p is { NodeId: "w", Kind: StrategyProblemKind.WaitDoesNotReturn });

        var lonely = Run([Node("e2", StrategyNodeType.Trigger, M(2026, 10))], months: 6);
        Assert.Contains(lonely.Problems, p => p is { NodeId: "e2", Kind: StrategyProblemKind.EventWithoutChain });
    }

    [Fact]
    public void A_second_loan_is_flagged_as_a_duplicate_and_ignored()
    {
        var result = Run(
        [
            Loan(balance: 1_000m, rate: 0m, installment: 1_000m),
            Node("loan2", StrategyNodeType.Loan, amount: 99_000m, rate: 0m, installment: 1m),
        ], months: 6);

        Assert.Contains(result.Problems, p => p is { NodeId: "loan2", Kind: StrategyProblemKind.Duplicate });
        Assert.Equal(0m, result.Months[^1].Debt);
    }

    [Fact]
    public void The_cushion_is_reached_only_once_the_debt_is_gone()
    {
        var result = Run(
        [
            Node("s", StrategyNodeType.Surplus, amount: 2_000m),
            Loan(balance: 6_000m, rate: 0m, installment: 1_000m),
            Node("cushion", StrategyNodeType.CushionGoal, amount: 5_000m),
        ], months: 12);

        Assert.Equal(M(2027, 3), result.LoanPaidOffIn);
        Assert.Equal(M(2027, 3), result.CushionReachedIn);
    }

    [Fact]
    public void Actions_that_only_exist_in_the_app_have_no_effect_in_the_simulation()
    {
        var result = Run(
        [
            Node("e", StrategyNodeType.Trigger, M(2026, 10)),
            Node("goal", StrategyNodeType.SetSavingsGoal, amount: 800m),
        ], [Edge("e", "goal")], cash: 250m, months: 6);

        Assert.Equal(250m, result.FinalCash);
        Assert.Equal(M(2026, 10), result.Nodes.Single(n => n.NodeId == "goal").FiredIn);
    }

    [Fact]
    public void Problem_kind_goes_to_the_client_as_a_NAME_not_a_number()
    {
        // Łapie brak konwertera na enumie: front mapuje nazwę na komunikat, więc liczba dawała surowy klucz
        // tłumaczenia („strategies.board.problem.2”) zamiast opisu problemu.
        var json = System.Text.Json.JsonSerializer.Serialize(
            new Features.Strategies.Contracts.StrategyProblemResponseDto("plus", StrategyProblemKind.NoIncomingEdge));

        Assert.Contains("\"NoIncomingEdge\"", json);
    }

    private static StrategyResult RunDisabled(
        IReadOnlyList<StrategyNode> nodes, IReadOnlyList<StrategyEdge> edges, decimal cash, params string[] disabled) =>
        StrategySimulator.Run(new StrategyInput(October2026, cash, 6, nodes, edges, disabled.ToHashSet()));

    [Fact]
    public void A_disabled_surplus_tile_adds_nothing_while_the_base_variant_keeps_it()
    {
        var nodes = new[] { Node("s", StrategyNodeType.Surplus, amount: 700m) };

        Assert.Equal(1_000m + 6 * 700m, RunDisabled(nodes, [], 1_000m).FinalCash);
        Assert.Equal(1_000m, RunDisabled(nodes, [], 1_000m, "s").FinalCash);
    }

    [Fact]
    public void Disabling_an_event_also_skips_the_chain_that_starts_from_it()
    {
        // Łapie błąd, w którym wyłączone zdarzenie nie wykonuje się, ale jego akcje dalej odpalają — wariant „bez podwyżki”
        // musiałby wtedy wyłączać każdą akcję z osobna.
        var nodes = new[]
        {
            Node("raise", StrategyNodeType.Trigger, M(2026, 10)),
            Node("more", StrategyNodeType.IncreaseSurplus, amount: 400m),
            Node("s", StrategyNodeType.Surplus, amount: 700m),
        };
        StrategyEdge[] edges = [Edge("raise", "more")];

        var withRaise = RunDisabled(nodes, edges, 0m);
        var withoutRaise = RunDisabled(nodes, edges, 0m, "raise");

        Assert.Equal(6 * 1_100m, withRaise.FinalCash);
        Assert.Equal(6 * 700m, withoutRaise.FinalCash);
        Assert.DoesNotContain(withoutRaise.Nodes, n => n.NodeId == "more");
    }

    [Fact]
    public void A_disabled_tile_does_not_hide_or_add_graph_problems()
    {
        // Problemy liczone są dla całego grafu: wariant nie może ich chować, bo wynik wariantu stałby się
        // „czystszy” niż strategia, którą użytkownik zapisze.
        var nodes = new[] { Node("orphan", StrategyNodeType.IncreaseSurplus, amount: 100m) };

        var baseProblems = RunDisabled(nodes, [], 0m).Problems;
        var variantProblems = RunDisabled(nodes, [], 0m, "orphan").Problems;

        Assert.Equal(baseProblems.Select(p => (p.NodeId, p.Kind)), variantProblems.Select(p => (p.NodeId, p.Kind)));
        Assert.NotEmpty(baseProblems);
    }

    private static StrategyNode Fact(StrategyNode plan, DateOnly month, decimal? amount) => plan with { ActualMonth = month, ActualAmount = amount };

    [Fact]
    public void A_realized_income_counts_in_the_actual_month_with_the_actual_amount_instead_of_the_plan()
    {
        // Łapie symulację, która dalej liczy premię z planu (maj, 7 800 zł), choć wpłynęła w czerwcu i w innej kwocie.
        var plan = Node("bonus", StrategyNodeType.Income, M(2027, 5), 7_800m);

        var planned = Run([plan], cash: 0m, months: 12);
        var realized = Run([Fact(plan, M(2027, 6), 8_150m)], cash: 0m, months: 12);

        Assert.Equal(0m, planned.Months.Single(m => m.Month == M(2027, 4)).Cash);
        Assert.Equal(7_800m, planned.Months.Single(m => m.Month == M(2027, 5)).Cash);
        Assert.Equal(0m, realized.Months.Single(m => m.Month == M(2027, 5)).Cash);
        Assert.Equal(8_150m, realized.Months.Single(m => m.Month == M(2027, 6)).Cash);
        Assert.Equal(M(2027, 6), realized.Nodes.Single(n => n.NodeId == "bonus").FiredIn);
    }

    [Fact]
    public void A_realized_expense_and_its_chain_move_to_the_actual_month()
    {
        var plan = Node("ins", StrategyNodeType.Expense, M(2026, 12), 2_200m);
        var edges = new[] { Edge("ins", "more") };
        var nodes = new[] { Fact(plan, M(2027, 1), 2_350m), Node("more", StrategyNodeType.IncreaseSurplus, amount: 100m) };

        var result = Run(nodes, edges, cash: 5_000m, months: 6);

        Assert.Equal(M(2027, 1), result.Nodes.Single(n => n.NodeId == "more").FiredIn);
        Assert.Equal(5_000m, result.Months.Single(m => m.Month == M(2026, 12)).Cash);
        Assert.Equal(5_000m - 2_350m + 100m, result.Months.Single(m => m.Month == M(2027, 1)).Cash);
    }

    [Fact]
    public void A_realized_income_without_the_actual_amount_is_a_problem_and_is_skipped()
    {
        // Nie zgadujemy kwoty faktu z planu: wpływ „nastąpił”, ale bez kwoty, nie może po cichu liczyć się z planu.
        var broken = Fact(Node("bonus", StrategyNodeType.Income, M(2027, 5), 7_800m), M(2027, 6), null);

        var result = Run([broken], cash: 0m, months: 12);

        Assert.Contains(result.Problems, p => p.NodeId == "bonus" && p.Kind == StrategyProblemKind.MissingParameter);
        Assert.Equal(0m, result.FinalCash);
    }

    [Fact]
    public void A_realized_trigger_fires_its_chain_in_the_actual_month_without_an_amount()
    {
        var trigger = Fact(Node("raise", StrategyNodeType.Trigger, M(2027, 1)), M(2027, 3), null);
        var nodes = new[] { trigger, Node("more", StrategyNodeType.IncreaseSurplus, amount: 400m) };

        var result = Run(nodes, [Edge("raise", "more")], months: 8);

        Assert.Equal(M(2027, 3), result.Nodes.Single(n => n.NodeId == "more").FiredIn);
    }
}
