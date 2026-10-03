namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Lista strategii jednego budżetu z przełącznikiem budżetów.</summary>
/// <param name="SelectedBudgetId">Budżet listy; <c>null</c> tylko wtedy, gdy użytkownik nie ma żadnego budżetu.</param>
/// <param name="Budgets">Budżety do przełącznika.</param>
/// <param name="Strategies">Strategie wybranego budżetu, od ostatnio zmienionej.</param>
public sealed record StrategiesResponseDto(
    Guid? SelectedBudgetId,
    IReadOnlyList<StrategiesBudgetOptionResponseDto> Budgets,
    IReadOnlyList<StrategyListItemResponseDto> Strategies);
