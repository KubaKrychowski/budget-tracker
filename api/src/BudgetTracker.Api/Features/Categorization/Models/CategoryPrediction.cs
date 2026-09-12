using Microsoft.ML.Data;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>Wyjście modelu.</summary>
public sealed class CategoryPrediction
{
    [ColumnName("PredictedCategory")]
    public string Category { get; set; } = string.Empty;

    /// <summary>Rozkład prawdopodobieństw po wszystkich klasach — maksimum jest naszą pewnością.</summary>
    public float[] Score { get; set; } = [];

    /// <summary>
    /// Wektor cech WYLICZONYCH Z OPISU, wystawiony z wnętrza potoku.
    /// </summary>
    /// <remarks>
    /// Nie jest tu po to, żeby go pokazywać, tylko po to, żeby dało się sprawdzić rzecz,
    /// której sam <see cref="Score"/> nie mówi: czy opis w ogóle dotknął słownika modelu.
    /// Bez tego pewność 0,93 dla sprzedawcy, o którym model nie wie nic, jest nieodróżnialna
    /// od pewności 0,93 dla znanej sieci sklepów — patrz <c>MlCategorizer</c>.
    ///
    /// ⚠️ <see cref="VBuffer{T}"/>, nie <c>float[]</c>. Wektor ma kilkanaście tysięcy wymiarów
    /// i jest rzadki; tablica oznaczałaby ~50 kB kopiowanych na każdy wiersz importu.
    /// </remarks>
    [ColumnName("DescriptionFeatures")]
    public VBuffer<float> DescriptionFeatures { get; set; }
}
