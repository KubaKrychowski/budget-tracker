namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Zakończenie zlecenia stałego — dialog „Zakończ zlecenie”.</summary>
/// <param name="LastMonth">Dowolny dzień OSTATNIEGO miesiąca zlecenia; liczy się tylko rok i miesiąc.</param>
public sealed record EndStandingOrderRequestDto(DateOnly LastMonth);
