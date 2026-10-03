namespace BudgetTracker.Api.Features.Strategies.Exceptions;

/// <summary>Strategia bez nazwy albo z nazwą dłuższą niż 100 znaków — na liście byłaby wierszem bez adresata.</summary>
public sealed class StrategyNameRequiredException : Exception;
