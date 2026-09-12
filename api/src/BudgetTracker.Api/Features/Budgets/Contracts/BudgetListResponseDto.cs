namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>
/// Odpowiedź listy budżetów.
/// </summary>
/// <param name="RetentionDays">
/// Ile dni usunięty budżet daje się przywrócić. Jedzie razem z listą, bo modal usuwania
/// obiecuje konkretną liczbę dni — gdyby front trzymał ją u siebie, zmiana ustawienia
/// zamieniłaby ten komunikat w kłamstwo.
/// </param>
public sealed record BudgetListResponseDto(
    IReadOnlyList<BudgetListItemResponseDto> Budgets,
    int RetentionDays);
