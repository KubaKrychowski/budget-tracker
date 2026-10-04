using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Models;

namespace BudgetTracker.Api.Features.Strategies.Services;

/// <summary>
/// Liczy strategię miesiąc po miesiącu: gotówka i dług z kafelków zdarzeń, akcji i warunków.
/// </summary>
/// <remarks>
/// <para>
/// Czysta funkcja — bez bazy, zegara i losowości, więc ten sam graf daje zawsze ten sam wynik i da się ją testować
/// zmyślonymi scenariuszami. To SZACUNEK do planowania, nie porada: odsetki liczone wg daty rzeczywistej (rok 365 dni,
/// miesięczna kapitalizacja), bank może naliczać inaczej o kilka złotych.
/// </para>
/// <para>
/// <b>Kolejność w miesiącu</b> (ma znaczenie dla wyniku):
/// (1) start kredytu, (2) zdarzenia miesiąca i łańcuchy akcji, które z nich wynikają — na saldach z końca poprzedniego
/// miesiąca, (3) odsetki i rata, (4) nadwyżka miesięczna, (5) warunki sprawdzane na stanie z końca miesiąca i akcje,
/// które z nich wynikają, (6) zapis stanu. Dzięki (2) przed (3) nadpłata w maju liczy odsetki od niższego salda już za
/// maj, a podwyżka ze stycznia działa od stycznia.
/// </para>
/// <para>
/// ⚠️ Nadwyżka jest PO racie kredytu: rata zmniejsza dług, ale nie odejmuje gotówki (rata jest już w wydatkach, z których
/// powstaje nadwyżka). Tak liczy dokument, z którego wzięto scenariusz, i tak użytkownik wpisze „nadwyżkę”.
/// </para>
/// <para>
/// ⚠️ Węzeł wykonuje się najwyżej raz w miesiącu — chroni przed pętlą w obrębie miesiąca. „Czekaj” przenosi następniki na
/// kolejny miesiąc, więc jest jedyną dozwoloną pętlą. Węzły z problemami grafu (<see cref="StrategyGraphAnalyzer"/>) nie
/// wykonują się wcale.
/// </para>
/// </remarks>
public static class StrategySimulator
{
    private const int DaysInYear = 365;

    public static StrategyResult Run(StrategyInput input)
    {
        var problems = StrategyGraphAnalyzer.Analyze(input.Nodes, input.Edges);
        var blocked = problems
            .Where(p => p.Kind is StrategyProblemKind.MissingParameter or StrategyProblemKind.NoIncomingEdge
                or StrategyProblemKind.Cycle or StrategyProblemKind.Duplicate)
            .Select(p => p.NodeId)
            .ToHashSet();

        var byId = input.Nodes.ToDictionary(n => n.Id);
        var next = input.Edges
            .Where(e => byId.ContainsKey(e.From) && byId.ContainsKey(e.To))
            .ToLookup(e => e.From);

        var start = Normalize(input.StartMonth);
        var loan = input.Nodes.FirstOrDefault(n => n.Type == StrategyNodeType.Loan && !blocked.Contains(n.Id));
        var cushion = input.Nodes.FirstOrDefault(n => n.Type == StrategyNodeType.CushionGoal && !blocked.Contains(n.Id));
        var baseSurplus = input.Nodes
            .Where(n => n.Type == StrategyNodeType.Surplus && !blocked.Contains(n.Id))
            .Select(n => (From: n.Month is { } m ? Max(Normalize(m), start) : start, Amount: n.Amount ?? 0m))
            .ToList();
        var events = input.Nodes
            .Where(n => n.Type is StrategyNodeType.Trigger or StrategyNodeType.Income or StrategyNodeType.Expense
                && !blocked.Contains(n.Id) && n.Month is not null)
            .ToList();

        var cash = input.StartCash;
        var debt = 0m;
        var installment = 0m;
        var extraSurplus = 0m;
        var loanSeen = false;
        DateOnly? loanPaidOffIn = null;
        DateOnly? cushionReachedIn = null;
        var totalInterest = 0m;
        var firedIn = new Dictionary<string, DateOnly>();
        var metIn = new Dictionary<string, DateOnly>();
        var months = new List<StrategyMonthPoint>();
        var carried = new List<string>();

        for (var i = 0; i < input.HorizonMonths; i++)
        {
            var month = start.AddMonths(i);
            var queue = new Queue<string>(carried);
            carried = [];
            var visited = new HashSet<string>();
            var pendingConditions = new List<string>();

            if (loan is not null && !loanSeen && Max(Normalize(loan.Month ?? start), start) == month)
            {
                debt = loan.Amount!.Value;
                installment = loan.Installment!.Value;
                loanSeen = true;
            }

            foreach (var ev in events.Where(e => Normalize(e.Month!.Value) == month))
            {
                if (!visited.Add(ev.Id)) continue;
                if (ev.Type == StrategyNodeType.Income) cash += ev.Amount!.Value;
                if (ev.Type == StrategyNodeType.Expense) cash -= ev.Amount!.Value;
                firedIn.TryAdd(ev.Id, month);
                foreach (var edge in next[ev.Id]) queue.Enqueue(edge.To);
            }

            Drain();

            var interest = 0m;
            if (debt > 0m)
            {
                interest = Math.Round(
                    debt * (loan!.Rate!.Value / 100m) * DateTime.DaysInMonth(month.Year, month.Month) / DaysInYear, 2);
                var payment = Math.Min(installment, debt + interest);
                debt = Math.Round(debt + interest - payment, 2);
                totalInterest += interest;
            }

            cash += baseSurplus.Where(s => s.From <= month).Sum(s => s.Amount) + extraSurplus;

            while (pendingConditions.Count > 0)
            {
                var due = pendingConditions;
                pendingConditions = [];
                foreach (var id in due)
                {
                    var node = byId[id];
                    var met = Evaluate(node, cash, debt);
                    firedIn.TryAdd(id, month);
                    if (met) metIn.TryAdd(id, month);
                    var label = met ? StrategyEdgeLabel.Yes : StrategyEdgeLabel.No;
                    foreach (var edge in next[id].Where(e => e.Label == label)) queue.Enqueue(edge.To);
                }

                Drain();
            }

            if (loanSeen && debt == 0m && loanPaidOffIn is null) loanPaidOffIn = month;
            if (cushion is not null && cushionReachedIn is null && debt == 0m && cash >= cushion.Amount!.Value)
            {
                cushionReachedIn = month;
            }

            months.Add(new StrategyMonthPoint(month, cash, debt, interest));

            void Drain()
            {
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (blocked.Contains(id) || !visited.Add(id)) continue;
                    var node = byId[id];

                    switch (node.Type)
                    {
                        case StrategyNodeType.Condition:
                            pendingConditions.Add(id);
                            continue;
                        case StrategyNodeType.Wait:
                            firedIn.TryAdd(id, month);
                            carried.AddRange(next[id].Select(e => e.To));
                            continue;
                        case StrategyNodeType.IncreaseSurplus:
                            extraSurplus += node.Amount!.Value;
                            break;
                        case StrategyNodeType.Overpay:
                            Overpay(node);
                            break;
                        case StrategyNodeType.PayOffLoan:
                            PayOff();
                            break;
                        case StrategyNodeType.CreateEpisodicOrder:
                            cash -= node.Amount!.Value;
                            break;
                    }

                    firedIn.TryAdd(id, month);
                    foreach (var edge in next[id]) queue.Enqueue(edge.To);
                }
            }

            void Overpay(StrategyNode node)
            {
                var pay = Math.Min(node.Amount!.Value, Math.Min(Math.Max(cash, 0m), debt));
                if (pay <= 0m) return;
                var before = debt;
                cash -= pay;
                debt -= pay;
                if (debt <= 0m)
                {
                    installment = 0m;
                }
                else if ((node.Mode ?? OverpaymentMode.ReduceInstallment) == OverpaymentMode.ReduceInstallment)
                {
                    installment = Math.Round(installment * debt / before, 2);
                }
            }

            void PayOff()
            {
                var pay = Math.Min(Math.Max(cash, 0m), debt);
                if (pay <= 0m) return;
                cash -= pay;
                debt -= pay;
                if (debt <= 0m) installment = 0m;
            }
        }

        var outcomes = firedIn
            .Select(f => new StrategyNodeOutcome(f.Key, f.Value, metIn.TryGetValue(f.Key, out var met) ? met : null))
            .ToList();

        return new StrategyResult(
            months, loanPaidOffIn, cushionReachedIn, months.Count == 0 ? input.StartCash : months[^1].Cash,
            totalInterest, outcomes, problems);
    }

    private static bool Evaluate(StrategyNode node, decimal cash, decimal debt)
    {
        var value = node.Metric switch
        {
            StrategyConditionMetric.Cash => cash,
            StrategyConditionMetric.Debt => debt,
            _ => cash - debt,
        };

        return node.Comparison == StrategyConditionComparison.AtMost ? value <= node.Threshold!.Value : value >= node.Threshold!.Value;
    }

    private static DateOnly Normalize(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly Max(DateOnly a, DateOnly b) => a >= b ? a : b;
}
