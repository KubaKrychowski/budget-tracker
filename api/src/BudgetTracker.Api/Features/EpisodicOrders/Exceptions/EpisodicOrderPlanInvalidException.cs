namespace BudgetTracker.Api.Features.EpisodicOrders.Exceptions;

/// <summary>Zaplanowane zlecenie bez kategorii, dodatniej kwoty albo terminu — albo z nieznaną kategorią.</summary>
public sealed class EpisodicOrderPlanInvalidException : Exception;
