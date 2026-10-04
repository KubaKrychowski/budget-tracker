namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Wynik zastosowania strategii: co zrobiono, a co pominięto (bo już jest, czeka albo ma problem).</summary>
public sealed record ApplyStrategyResponseDto(IReadOnlyList<string> Applied, IReadOnlyList<string> Skipped);
