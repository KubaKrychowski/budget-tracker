using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Kafelek strategii w odpowiedzi — pola jak w <c>StrategyNode</c>.</summary>
public sealed record StrategyNodeResponseDto(
    string Id,
    StrategyNodeType Type,
    string Title,
    double X,
    double Y,
    DateOnly? Month,
    decimal? Amount,
    decimal? Rate,
    decimal? Installment,
    OverpaymentMode? Mode,
    StrategyConditionMetric? Metric,
    StrategyConditionComparison? Comparison,
    decimal? Threshold,
    Guid? CategoryId = null,
    Guid? StandingOrderId = null,
    DateOnly? ActualMonth = null,
    decimal? ActualAmount = null);
