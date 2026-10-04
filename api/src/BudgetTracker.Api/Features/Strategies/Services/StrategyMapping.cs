using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Models;

namespace BudgetTracker.Api.Features.Strategies.Services;

/// <summary>Przekształcenia między encją strategii, wejściem symulatora i kontraktem HTTP.</summary>
public static class StrategyMapping
{
    public static StrategyInput ToInput(Strategy strategy) =>
        new(strategy.StartMonth, strategy.StartCash, strategy.HorizonMonths, strategy.Nodes, strategy.Edges);

    public static StrategyInput ToInput(ValidStrategy valid) =>
        new(valid.StartMonth, valid.StartCash, valid.HorizonMonths, valid.Nodes, valid.Edges);

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
                n.Comparison, n.Threshold, n.CategoryId, n.StandingOrderId))],
            [.. strategy.Edges.Select(e => new StrategyEdgeResponseDto(e.Id, e.From, e.To, e.Label))],
            ToResult(result));

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
