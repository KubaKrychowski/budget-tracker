namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Podgląd reguł przed zapisem — do ilu transakcji z historii budżetu już pasują.</summary>
/// <param name="StandingOrderId">
/// Zlecenie w edycji — jego ręczne odpięcia nie liczą się do podglądu, a przy zakończonym liczy się tylko historia do
/// ostatniego miesiąca.
/// </param>
/// <param name="Rules">Reguły z modala; puste frazy krótsze niż minimum to 400, jak przy zapisie.</param>
public sealed record StandingOrderPreviewRequestDto(
    Guid? BudgetId,
    Guid? StandingOrderId,
    IReadOnlyList<StandingOrderRuleRequestDto> Rules);
