namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>Zakres kwoty reguły jest ujemny, zerowy albo odwrócony.</summary>
public sealed class SavingsTransferAmountInvalidException : Exception;
