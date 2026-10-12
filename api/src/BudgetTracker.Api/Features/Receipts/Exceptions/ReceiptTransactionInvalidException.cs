namespace BudgetTracker.Api.Features.Receipts.Exceptions;

/// <summary>Transakcja wskazana w ciele żądania nie istnieje albo należy do cudzego zakresu.</summary>
public sealed class ReceiptTransactionInvalidException : Exception;
