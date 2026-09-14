namespace BudgetTracker.Api.Features.EpisodicOrders.Exceptions;

/// <summary>Zaplanowane zlecenie bez kategorii albo dodatniej kwoty — albo z nieznaną kategorią. Termin jest opcjonalny.</summary>
public sealed class EpisodicOrderPlanInvalidException : Exception;
