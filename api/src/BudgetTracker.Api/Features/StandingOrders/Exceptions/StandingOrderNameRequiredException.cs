namespace BudgetTracker.Api.Features.StandingOrders.Exceptions;

/// <summary>Zlecenie bez nazwy — na liście byłoby wierszem bez adresata.</summary>
public sealed class StandingOrderNameRequiredException : Exception;
