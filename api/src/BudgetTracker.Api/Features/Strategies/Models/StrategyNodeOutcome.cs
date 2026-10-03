namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Co się stało z węzłem w symulacji.</summary>
/// <param name="NodeId">Węzeł.</param>
/// <param name="FiredIn">Pierwszy miesiąc, w którym węzeł wykonał się albo został sprawdzony.</param>
/// <param name="ConditionMetIn">Dla warunku: pierwszy miesiąc, w którym był spełniony; <c>null</c> = nigdy lub nie warunek.</param>
public sealed record StrategyNodeOutcome(string NodeId, DateOnly? FiredIn, DateOnly? ConditionMetIn);
