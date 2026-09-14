namespace BudgetTracker.Api.Features.StandingOrders.Exceptions;

/// <summary>Zlecenie bez reguł albo z ich nadmiarem — bez reguły nic by się nie przypinało.</summary>
public sealed class StandingOrderRulesInvalidException : Exception;
