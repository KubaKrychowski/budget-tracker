using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Models;

namespace BudgetTracker.Api.Features.Strategies.Services;

/// <summary>Przekształcenia między encją strategii, wejściem symulatora i kontraktem HTTP.</summary>
public static class StrategyMapping
{
    public static StrategyInput ToInput(Strategy strategy, string? variantId = null) =>
        new(strategy.StartMonth, strategy.StartCash, strategy.HorizonMonths, strategy.Nodes, strategy.Edges,
            Disabled(strategy.Variants, variantId));

    public static StrategyInput ToInput(ValidStrategy valid, string? variantId = null) =>
        new(valid.StartMonth, valid.StartCash, valid.HorizonMonths, valid.Nodes, valid.Edges,
            Disabled(valid.Variants, variantId));

    /// <summary>Kafelki wyłączone w wariancie o danym identyfikatorze; brak wariantu = bazowy, czyli nic.</summary>
    /// <remarks>Nieznany identyfikator to bazowy, nie błąd: klient mógł właśnie usunąć wariant, który jeszcze wybiera.</remarks>
    public static IReadOnlySet<string> Disabled(IEnumerable<StrategyVariant> variants, string? variantId) =>
        variantId is null
            ? new HashSet<string>()
            : variants.FirstOrDefault(v => v.Id == variantId)?.DisabledNodeIds.ToHashSet() ?? [];

    public static StrategyResponseDto ToResponse(Strategy strategy, StrategyResult result) =>
        new(
            strategy.BusinessId,
            strategy.BudgetBusinessId,
            strategy.Name,
            strategy.StartMonth,
            strategy.StartCash,
            strategy.HorizonMonths,
            strategy.UpdatedAt,
            [.. strategy.Nodes.Select(n => new StrategyNodeResponseDto(
                n.Id, n.Type, n.Title, n.X, n.Y, n.Month, n.Amount, n.Rate, n.Installment, n.Mode, n.Metric,
                n.Comparison, n.Threshold, n.CategoryId, n.StandingOrderId, n.ActualMonth, n.ActualAmount))],
            [.. strategy.Edges.Select(e => new StrategyEdgeResponseDto(e.Id, e.From, e.To, e.Label))],
            ToResult(result),
            [.. strategy.Variants.Select(v => new StrategyVariantResponseDto(
                v.Id, v.Name, v.DisabledNodeIds, ToResult(StrategySimulator.Run(ToInput(strategy, v.Id)))))]);

    public static StrategyResultResponseDto ToResult(StrategyResult result) =>
        new(
            [.. result.Months.Select(m => new StrategyMonthResponseDto(m.Month, m.Cash, m.Debt, m.Interest))],
            result.LoanPaidOffIn,
            result.CushionReachedIn,
            result.FinalCash,
            result.TotalInterest,
            [.. result.Nodes.Select(n => new StrategyNodeOutcomeResponseDto(n.NodeId, n.FiredIn, n.ConditionMetIn))],
            [.. result.Problems.Select(p => new StrategyProblemResponseDto(p.NodeId, p.Kind))]);
}
