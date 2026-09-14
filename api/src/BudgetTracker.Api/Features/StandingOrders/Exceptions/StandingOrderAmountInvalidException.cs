namespace BudgetTracker.Api.Features.StandingOrders.Exceptions;

/// <summary>Kwota zwykła niedodatnia albo zakres kwot odwrócony („od” większe niż „do”).</summary>
public sealed class StandingOrderAmountInvalidException : Exception;
