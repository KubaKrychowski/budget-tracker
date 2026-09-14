namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>
/// Pełny stan wiersza w trybie edycji — inline edytuje się cały wiersz naraz (nie pojedyncze
/// pole), więc front zawsze wysyła komplet aktualnych wartości kontrolek, nie tylko różnicę.
/// </summary>
public sealed record TransactionEditRequestDto(
    Guid Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    Guid? CategoryId);
