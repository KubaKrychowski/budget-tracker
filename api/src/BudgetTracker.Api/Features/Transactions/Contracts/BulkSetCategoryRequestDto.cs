namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Masowe ustawienie kategorii — liczy się jako ręczna korekta człowieka.</summary>
/// <param name="CategoryId">Publiczny <c>BusinessId</c> kategorii.</param>
public sealed record BulkSetCategoryRequestDto(TransactionSelectionRequestDto Selection, Guid CategoryId);
