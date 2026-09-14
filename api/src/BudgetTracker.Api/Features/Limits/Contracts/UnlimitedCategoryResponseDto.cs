namespace BudgetTracker.Api.Features.Limits.Contracts;

/// <summary>Kategoria z wydatkami w oglądanym miesiącu, która nie ma w nim limitu — karta „Wydatki bez limitu".</summary>
public sealed record UnlimitedCategoryResponseDto(Guid CategoryId, string CategoryName, decimal Spent);
