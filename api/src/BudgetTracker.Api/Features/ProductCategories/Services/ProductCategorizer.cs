using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Features.ProductCategories.Models;

namespace BudgetTracker.Api.Features.ProductCategories.Services;

/// <summary>Reguły, potem model — ta sama kolejność co przy transakcjach (<c>HybridCategorizer</c>).</summary>
/// <remarks>
/// Reguła jest pewna z definicji, więc model nigdy jej nie podważa. Model dostaje tylko to, czego reguły nie złapały, a jego
/// podpowiedź wchodzi dopiero powyżej progu pewności z konfiguracji kategoryzacji (<see cref="ImportConfidenceThreshold"/>) — poniżej
/// pozycja zostaje bez kategorii i czeka na wybór użytkownika, zamiast dostać zgadniętą.
/// </remarks>
public sealed class ProductCategorizer(
    ProductRuleCategorizer rules, ProductMlCategorizer ml, ImportConfidenceThreshold threshold)
{
    public async Task<ProductSuggestion> CategorizeAsync(string productName, CancellationToken ct)
    {
        var normalized = ProductNameNormalizer.Normalize(productName);

        if (await rules.CategorizeAsync(normalized, ct) is { } byRule) return new ProductSuggestion(byRule, 1m);

        var byModel = await ml.CategorizeAsync(normalized, ct);
        return byModel.CategoryId is not null && threshold.IsConfident(byModel.Confidence) ? byModel : ProductSuggestion.None;
    }
}
