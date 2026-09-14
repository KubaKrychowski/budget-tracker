namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Podgląd reguły przed zapisem — do ilu transakcji z historii budżetu już pasuje.</summary>
/// <param name="StandingOrderId">Zlecenie w edycji — jego ręczne odpięcia nie liczą się do podglądu.</param>
public sealed record StandingOrderPreviewRequestDto(
    Guid? BudgetId,
    Guid? StandingOrderId,
    string TitlePattern,
    decimal AmountFrom,
    decimal AmountTo);
