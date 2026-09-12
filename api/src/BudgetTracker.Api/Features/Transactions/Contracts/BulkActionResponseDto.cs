namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Wynik akcji masowej: ile wierszy faktycznie zmieniono.</summary>
public sealed record BulkActionResponseDto(int Affected);
