namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Masowe usunięcie (logiczne) transakcji z zasięgu <paramref name="Selection"/>.</summary>
public sealed record BulkDeleteRequestDto(TransactionSelectionRequestDto Selection);
