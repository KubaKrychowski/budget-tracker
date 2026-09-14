namespace BudgetTracker.Api.Features.Limits.Exceptions;

/// <summary>
/// Zmiana dotknęłaby zamkniętego miesiąca — limity zmienia się od bieżącego miesiąca w przód.
/// </summary>
/// <remarks>409: żądanie jest poprawne, to stan historii limitów na nie nie pozwala.</remarks>
public sealed class LimitHistoryLockedException : Exception;
