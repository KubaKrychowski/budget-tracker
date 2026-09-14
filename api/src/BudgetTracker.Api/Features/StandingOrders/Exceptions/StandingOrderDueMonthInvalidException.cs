namespace BudgetTracker.Api.Features.StandingOrders.Exceptions;

/// <summary>Zlecenie roczne albo kwartalne bez miesiąca (1–12) — nie wiadomo, kiedy ma zejść.</summary>
public sealed class StandingOrderDueMonthInvalidException : Exception;
