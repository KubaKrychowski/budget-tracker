namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Założenie albo edycja rezerwacji.</summary>
/// <param name="DueMonth">Dowolny dzień miesiąca — serwer normalizuje do pierwszego.</param>
/// <param name="BudgetId">
/// JEDEN budżet, nie lista — tak samo jak przy celu. „Załóż tę rezerwację na trzech naraz"
/// nie ma znaczenia, które dałoby się obronić. Przy edycji ignorowany.
/// </param>
public sealed record SaveReservationRequestDto(
    string Name, decimal Amount, DateOnly DueMonth, int Priority = 0, Guid? BudgetId = null);
