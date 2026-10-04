using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Models;

namespace BudgetTracker.Api.Features.Strategies.Services;

/// <summary>Gotowe grafy startowe nowej strategii.</summary>
/// <remarks>
/// ⚠️ Liczby w szablonie są PRZYKŁADOWE i zmyślone — mają pokazać kształt (kredyt, premia, nadpłata, warunek, poduszka),
/// nie czyjąkolwiek sytuację. Nie ma w nim akcji, które wymagają wskazania konkretnego obiektu użytkownika (zlecenie stałe,
/// kategoria) — szablon ma działać dla każdego bez dopisywania parametrów. Podpisy kafelków idą przez <paramref name="text"/>, bo są tekstem dla użytkownika
/// (klucze <c>Strategy_Template_*</c> w zasobach); test podaje tożsamość.
/// </remarks>
public static class StrategyTemplates
{
    public static StrategyTemplateGraph Build(StrategyTemplate template, DateOnly startMonth, Func<string, string> text) =>
        template == StrategyTemplate.LoanAndCushion ? LoanAndCushion(startMonth, text) : new StrategyTemplateGraph(0m, [], []);

    private static StrategyTemplateGraph LoanAndCushion(DateOnly start, Func<string, string> text)
    {
        StrategyNode Node(
            string id, StrategyNodeType type, string key, double x, double y, DateOnly? month = null,
            decimal? amount = null, decimal? rate = null, decimal? installment = null, OverpaymentMode? mode = null,
            StrategyConditionMetric? metric = null, StrategyConditionComparison? comparison = null,
            decimal? threshold = null) =>
            new(id, type, text(key), x, y, month, amount, rate, installment, mode, metric, comparison, threshold);

        StrategyEdge Edge(string from, string to, StrategyEdgeLabel label = StrategyEdgeLabel.None) =>
            new($"{from}-{to}", from, to, label);

        DateOnly In(int months) => start.AddMonths(months);

        const double c1 = 24, c2 = 304, c3 = 584, c4 = 864;

        StrategyNode[] nodes =
        [
            Node("surplus", StrategyNodeType.Surplus, "Strategy_Template_Surplus", c1, 0, amount: 700m),
            Node("loan", StrategyNodeType.Loan, "Strategy_Template_Loan", c2, 0, start, 18_400m, 12m, 880m),
            Node("cushion", StrategyNodeType.CushionGoal, "Strategy_Template_Cushion", c3, 0, amount: 15_000m),

            Node("bonus", StrategyNodeType.Income, "Strategy_Template_Bonus", c1, 140, In(7), 7_800m),
            Node("overpay", StrategyNodeType.Overpay, "Strategy_Template_Overpay", c2, 140, amount: 7_000m,
                mode: OverpaymentMode.ReduceInstallment),
            Node("check", StrategyNodeType.Condition, "Strategy_Template_Check", c3, 140,
                metric: StrategyConditionMetric.CashMinusDebt, comparison: StrategyConditionComparison.AtLeast,
                threshold: 9_000m),
            Node("wait", StrategyNodeType.Wait, "Strategy_Template_Wait", c3, 280),
            Node("payoff", StrategyNodeType.PayOffLoan, "Strategy_Template_PayOff", c4, 140),
            Node("goal", StrategyNodeType.SetSavingsGoal, "Strategy_Template_Goal", c4, 280, amount: 1_100m),
            Node("end", StrategyNodeType.End, "Strategy_Template_End", c4, 420),

            Node("raise", StrategyNodeType.Trigger, "Strategy_Template_Raise", c1, 420, In(3)),
            Node("raiseSurplus", StrategyNodeType.IncreaseSurplus, "Strategy_Template_RaiseSurplus", c2, 420, amount: 400m),

            Node("insurance", StrategyNodeType.Expense, "Strategy_Template_Insurance", c1, 560, In(2), 2_200m),
            Node("reservation", StrategyNodeType.CreateReservation, "Strategy_Template_Reservation", c2, 560,
                amount: 2_200m),
        ];

        StrategyEdge[] edges =
        [
            Edge("bonus", "overpay"),
            Edge("overpay", "check"),
            Edge("check", "payoff", StrategyEdgeLabel.Yes),
            Edge("check", "wait", StrategyEdgeLabel.No),
            Edge("wait", "check"),
            Edge("payoff", "goal"),
            Edge("goal", "end"),
            Edge("raise", "raiseSurplus"),
            Edge("insurance", "reservation"),
        ];

        return new StrategyTemplateGraph(4_600m, nodes, edges);
    }
}
