namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>
/// Wynik kategoryzacji jednej transakcji.
/// </summary>
/// <param name="CategoryId">Null, gdy nie udało się przypisać kategorii.</param>
/// <param name="Confidence">
/// Pewność 0–1. Reguła zwraca 1.0 (dopasowanie wzorca jest pewne), model — swoją ocenę.
/// Null, gdy nie było predykcji.
/// </param>
public readonly record struct CategorySuggestion(int? CategoryId, decimal? Confidence)
{
    public static CategorySuggestion None => new(null, null);

    /// <summary>Dopasowanie regułą — pewność z definicji pełna, nie ma tu miejsca na wątpliwość.</summary>
    public static CategorySuggestion FromRule(int categoryId) => new(categoryId, 1.0m);
}
