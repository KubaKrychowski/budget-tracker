namespace BudgetTracker.Api.Features.EpisodicOrders.Contracts;

/// <summary>Wiersz tabeli zleceń epizodycznych — zaplanowanego albo zrealizowanego.</summary>
/// <param name="CategoryId">Kategoria planu (do edycji); przy zrealizowanym bez planu <c>null</c>.</param>
/// <param name="CategoryName">Przy zrealizowanym — kategoria TRANSAKCJI, przy zaplanowanym — planu.</param>
/// <param name="Amount">Dodatnia. Przy zrealizowanym — kwota transakcji, przy zaplanowanym — planu.</param>
/// <param name="DueMonth">Termin planu; <c>null</c> — bez terminu („przy okazji”) albo bez planu.</param>
/// <param name="Date">Data transakcji; <c>null</c> przy zaplanowanym.</param>
/// <param name="WasPlanned">Tylko takie zrealizowane zlecenie da się „cofnąć do zaplanowanych”.</param>
/// <param name="Collected">Uzbierane w rezerwacji — przydział z kolejki zbierania, jak na ekranie rezerwacji.</param>
public sealed record EpisodicOrderRowResponseDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    decimal Amount,
    DateOnly? DueMonth,
    DateOnly? Date,
    Guid? TransactionId,
    string? TransactionDescription,
    bool WasPlanned,
    Guid? ReservationId,
    decimal? Collected);
