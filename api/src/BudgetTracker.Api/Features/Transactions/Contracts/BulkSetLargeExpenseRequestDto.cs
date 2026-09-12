namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Masowe ustawienie albo zdjęcie flagi „duży wydatek".</summary>
public sealed record BulkSetLargeExpenseRequestDto(TransactionSelectionRequestDto Selection, bool IsLargeExpense);
