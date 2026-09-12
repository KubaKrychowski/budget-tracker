namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Rezerwacja bez nazwy nie jest kopertą, tylko kwotą bez adresata.</summary>
public sealed class ReservationNameRequiredException : Exception;
