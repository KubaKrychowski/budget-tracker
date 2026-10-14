namespace BudgetTracker.Api.Features.ProductCategories.Models;

/// <summary>Wynik kategoryzacji jednego produktu.</summary>
/// <param name="CategoryId"><c>BusinessId</c> kategorii produktu; <c>null</c>, gdy nie udało się przypisać.</param>
/// <param name="Confidence">Pewność 0–1: reguła daje 1, model swoją ocenę; <c>null</c> bez predykcji.</param>
public readonly record struct ProductSuggestion(Guid? CategoryId, decimal? Confidence)
{
    public static ProductSuggestion None => new(null, null);
}
