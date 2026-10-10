namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Wynik symulacji jednego wariantu niezapisanego grafu.</summary>
/// <param name="Id">Identyfikator wariantu z żądania.</param>
/// <param name="Result">Wynik symulacji grafu bez wyłączonych kafelków tego wariantu.</param>
public sealed record StrategyVariantResultResponseDto(string Id, StrategyResultResponseDto Result);
