namespace BudgetTracker.Api.Features.Limits.Exceptions;

/// <summary>Próg ostrzeżenia poza zakresem 1–100% — patrz <c>LimitWarning</c>.</summary>
public sealed class LimitWarningThresholdInvalidException : Exception;
