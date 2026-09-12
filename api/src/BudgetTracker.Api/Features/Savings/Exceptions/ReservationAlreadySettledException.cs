namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Rezerwacja jest już rozliczona — 409, bo żądanie jest poprawne, tylko stan na nie nie pozwala.</summary>
public sealed class ReservationAlreadySettledException : Exception;
