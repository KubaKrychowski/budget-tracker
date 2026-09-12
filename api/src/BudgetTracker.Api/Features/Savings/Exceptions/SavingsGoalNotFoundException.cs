namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Rezygnacja z celu, gdy żadnego nie ma — 404, a nie ciche „ok".</summary>
public sealed class SavingsGoalNotFoundException : Exception;
