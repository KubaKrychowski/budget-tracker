namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Co się stało z węzłem w symulacji — do panelu węzła („spełniony w lipcu 2027”).</summary>
/// <param name="NodeId">Węzeł.</param>
/// <param name="FiredIn">Pierwszy miesiąc wykonania albo sprawdzenia.</param>
/// <param name="ConditionMetIn">Dla warunku: pierwszy miesiąc, w którym był spełniony.</param>
public sealed record StrategyNodeOutcomeResponseDto(string NodeId, DateOnly? FiredIn, DateOnly? ConditionMetIn);
