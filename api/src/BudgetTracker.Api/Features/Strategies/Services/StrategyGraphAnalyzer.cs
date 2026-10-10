using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Models;

namespace BudgetTracker.Api.Features.Strategies.Services;

/// <summary>Szuka problemów w grafie strategii — brakujących parametrów, osieroconych węzłów, cykli.</summary>
/// <remarks>
/// <para>
/// Czysta funkcja bez bazy. Zakłada graf STRUKTURALNIE poprawny (unikalne identyfikatory, połączenia wskazują istniejące
/// węzły) — to pilnuje <see cref="StrategyGraphValidator"/> przy zapisie. Problemy tutaj są informacją dla użytkownika.
/// </para>
/// <para>
/// ⚠️ Cykl przez „Czekaj” jest dozwolony: to jedyna pętla (warunek → „nie” → czekaj → warunek). Krawędzie wychodzące
/// z „Czekaj” są więc przy szukaniu cykli pomijane.
/// </para>
/// </remarks>
public static class StrategyGraphAnalyzer
{
    public static IReadOnlyList<StrategyProblem> Analyze(
        IReadOnlyList<StrategyNode> nodes, IReadOnlyList<StrategyEdge> edges)
    {
        var problems = new List<StrategyProblem>();
        var known = nodes.Select(n => n.Id).ToHashSet();
        var valid = edges.Where(e => known.Contains(e.From) && known.Contains(e.To)).ToList();
        var incoming = valid.Select(e => e.To).ToHashSet();
        var outgoing = valid.Select(e => e.From).ToHashSet();

        foreach (var node in nodes)
        {
            if (HasMissingParameter(node)) problems.Add(new StrategyProblem(node.Id, StrategyProblemKind.MissingParameter));
            if (NeedsIncomingEdge(node.Type) && !incoming.Contains(node.Id))
            {
                problems.Add(new StrategyProblem(node.Id, StrategyProblemKind.NoIncomingEdge));
            }

            if (node.Type == StrategyNodeType.Trigger && !outgoing.Contains(node.Id))
            {
                problems.Add(new StrategyProblem(node.Id, StrategyProblemKind.EventWithoutChain));
            }

            if (node.Type == StrategyNodeType.Wait && !outgoing.Contains(node.Id))
            {
                problems.Add(new StrategyProblem(node.Id, StrategyProblemKind.WaitDoesNotReturn));
            }
        }

        foreach (var id in FindCycleNodes(nodes, valid)) problems.Add(new StrategyProblem(id, StrategyProblemKind.Cycle));
        foreach (var id in FindDuplicates(nodes)) problems.Add(new StrategyProblem(id, StrategyProblemKind.Duplicate));

        return problems;
    }

    /// <summary>Węzły, do których musi prowadzić strzałka: wszystko poza zdarzeniami i węzłami bazowymi.</summary>
    private static bool NeedsIncomingEdge(StrategyNodeType type) => type is not (
        StrategyNodeType.Trigger or StrategyNodeType.Income or StrategyNodeType.Expense
        or StrategyNodeType.Surplus or StrategyNodeType.Loan or StrategyNodeType.CushionGoal);

    private static bool HasMissingParameter(StrategyNode node) => node.Type switch
    {
        StrategyNodeType.Trigger => node.Month is null,
        StrategyNodeType.Income or StrategyNodeType.Expense =>
            node.Month is null || node.Amount is not > 0m || (node.ActualMonth is not null && node.ActualAmount is not > 0m),
        StrategyNodeType.Surplus => node.Amount is null,
        StrategyNodeType.Loan => node.Amount is not > 0m || node.Rate is null or < 0m || node.Installment is not > 0m,
        StrategyNodeType.CushionGoal => node.Amount is not > 0m,
        StrategyNodeType.IncreaseSurplus => node.Amount is null or 0m,
        StrategyNodeType.Overpay => node.Amount is not > 0m,
        StrategyNodeType.SetSavingsGoal => node.Amount is not > 0m,
        StrategyNodeType.CreateReservation => node.Amount is not > 0m || string.IsNullOrWhiteSpace(node.Title),
        StrategyNodeType.SetLimit => node.Amount is not > 0m || node.CategoryId is null,
        StrategyNodeType.EndStandingOrder => node.StandingOrderId is null || node.Month is null,
        StrategyNodeType.CreateEpisodicOrder =>
            node.Amount is not > 0m || node.CategoryId is null || string.IsNullOrWhiteSpace(node.Title),
        StrategyNodeType.Condition => node.Metric is null || node.Comparison is null || node.Threshold is null,
        _ => false,
    };

    /// <summary>Drugi i kolejne kredyty oraz poduszki — symulator liczy tylko pierwszy z każdego rodzaju.</summary>
    private static IEnumerable<string> FindDuplicates(IReadOnlyList<StrategyNode> nodes) =>
        nodes.Where(n => n.Type == StrategyNodeType.Loan).Skip(1).Concat(
            nodes.Where(n => n.Type == StrategyNodeType.CushionGoal).Skip(1)).Select(n => n.Id);

    /// <summary>Węzły na cyklach, które nie przechodzą przez „Czekaj” (jego wyjścia są pomijane).</summary>
    private static HashSet<string> FindCycleNodes(IReadOnlyList<StrategyNode> nodes, List<StrategyEdge> edges)
    {
        var waits = nodes.Where(n => n.Type == StrategyNodeType.Wait).Select(n => n.Id).ToHashSet();
        var next = edges.Where(e => !waits.Contains(e.From)).ToLookup(e => e.From, e => e.To);
        var onCycle = new HashSet<string>();
        var done = new HashSet<string>();

        void Visit(string id, List<string> path)
        {
            if (done.Contains(id)) return;
            var at = path.IndexOf(id);
            if (at >= 0)
            {
                foreach (var member in path.Skip(at)) onCycle.Add(member);
                return;
            }

            path.Add(id);
            foreach (var target in next[id]) Visit(target, path);
            path.RemoveAt(path.Count - 1);
            done.Add(id);
        }

        foreach (var node in nodes) Visit(node.Id, []);
        return onCycle;
    }
}
