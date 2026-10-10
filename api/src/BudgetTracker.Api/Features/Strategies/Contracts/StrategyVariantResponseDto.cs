namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Wariant zapisanej strategii razem z wynikiem jego symulacji.</summary>
/// <param name="Id">Identyfikator wariantu.</param>
/// <param name="Name">Nazwa wariantu.</param>
/// <param name="DisabledNodeIds">Kafelki wyłączone w tym wariancie.</param>
/// <param name="Result">Wynik symulacji grafu bez wyłączonych kafelków.</param>
public sealed record StrategyVariantResponseDto(
    string Id,
    string Name,
    IReadOnlyList<string> DisabledNodeIds,
    StrategyResultResponseDto Result);
