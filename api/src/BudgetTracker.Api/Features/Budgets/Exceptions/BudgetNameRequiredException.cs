namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>Nazwa budżetu jest wymagana — pusta nie odróżnia go w selektorze od żadnego innego.</summary>
public sealed class BudgetNameRequiredException : Exception;
