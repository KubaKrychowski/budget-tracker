namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Obiekty budżetu strategii, na które mogą wskazywać kafelki: kategorie i niezakończone zlecenia stałe.</summary>
public sealed record StrategyReferencesResponseDto(
    IReadOnlyList<StrategyCategoryOptionResponseDto> Categories,
    IReadOnlyList<StrategyStandingOrderOptionResponseDto> StandingOrders);
