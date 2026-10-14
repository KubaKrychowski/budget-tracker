using Microsoft.ML.Data;

namespace BudgetTracker.Api.Features.ProductCategories.Models;

/// <summary>Wejście modelu produktów: znormalizowana nazwa i kategoria (jako <c>BusinessId</c> tekstem) przy uczeniu.</summary>
public sealed class ProductFeatures
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}

/// <summary>Wyjście modelu produktów.</summary>
public sealed class ProductPrediction
{
    [ColumnName("PredictedCategory")]
    public string Category { get; set; } = string.Empty;

    public float[] Score { get; set; } = [];

    /// <summary>Wektor cech z nazwy — pusty oznacza, że model nie zna żadnego fragmentu nazwy (patrz <c>ProductMlCategorizer</c>).</summary>
    [ColumnName("NameFeatures")]
    public VBuffer<float> NameFeatures { get; set; }
}
