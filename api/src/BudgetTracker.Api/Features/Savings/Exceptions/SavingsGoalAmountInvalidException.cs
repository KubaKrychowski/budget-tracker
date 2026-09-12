namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Kwota celu musi być dodatnia — „odkładam 0 zł" nie jest celem, tylko jego brakiem.</summary>
public sealed class SavingsGoalAmountInvalidException : Exception;
