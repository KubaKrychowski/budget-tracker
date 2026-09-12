using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms.Text;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Uczenie modelu kategoryzacji. Klasyczne ML, nie deep learning — opisy transakcji to krótki,
/// zaszumiony tekst, więc TF-IDF na n-gramach plus klasyfikator liniowy jest właściwym narzędziem
/// (CLAUDE.md §3).
/// </summary>
public static class CategoryModelTrainer
{
    /// <summary>
    /// Uczy model na pliku CSV z kolumnami: opis, typ_transakcji, kwota, kategoria.
    /// </summary>
    /// <remarks>
    /// Zostaje dla treningu z samego pliku (i dla testów). Ekran „Dane treningowe" woła
    /// przeciążenie na gotowym zbiorze — patrz <see cref="TrainingSetBuilder"/>.
    /// </remarks>
    public static TrainingReportResponseDto Train(string dataPath, string modelPath, int? seed = 20260902)
    {
        var ml = new MLContext(seed);

        var all = ml.Data.LoadFromTextFile<TransactionFeatures>(
            dataPath, separatorChar: ',', hasHeader: true, allowQuoting: true, trimWhitespace: true);

        return Train(ml, all, modelPath, seed);
    }

    /// <summary>
    /// Uczy model na GOTOWYM zbiorze — bez pliku pośredniego.
    /// </summary>
    /// <remarks>
    /// Zbiór powstaje dziś z dwóch źródeł (plik bazowy + poprawki z bazy), więc zapisywanie go
    /// do CSV tylko po to, żeby ML.NET zaraz go wczytał, byłoby okrężną drogą przez dysk
    /// i kolejnym miejscem, w którym parsowanie mogłoby się rozjechać.
    /// </remarks>
    public static TrainingReportResponseDto Train(
        IEnumerable<TransactionFeatures> rows, string modelPath, int? seed = 20260902)
    {
        var ml = new MLContext(seed);
        return Train(ml, ml.Data.LoadFromEnumerable(rows), modelPath, seed);
    }

    /// <summary>Potok cech, uczenie, ewaluacja na odłożonej części i zapis modelu.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>20% na test. Bez tego „trafność" liczyłaby się na danych, które model widział,
    /// i pokazywałaby liczbę bez związku z zachowaniem na nowym wyciągu.</item>
    /// <item>Char n-gramy są kluczowe: dają odporność na literówki i sklejone kody
    /// sprzedawców w rodzaju „JMP S.A. BIEDRONKA 490" (CLAUDE.md §3).</item>
    /// <item>Kwota jest normalizowana — bez tego sama skala (setki złotych wobec wartości 0–1 z TF-IDF)
    /// zdominowałaby tysiące cech tekstowych.</item>
    /// </list>
    /// </remarks>
    private static TrainingReportResponseDto Train(MLContext ml, IDataView all, string modelPath, int? seed)
    {
        var split = ml.Data.TrainTestSplit(all, testFraction: 0.2, seed: seed);

        var pipeline = ml.Transforms.Conversion
            .MapValueToKey("Label", nameof(TransactionFeatures.Category))

            .Append(ml.Transforms.Text.FeaturizeText(
                "DescriptionFeatures",
                new TextFeaturizingEstimator.Options
                {
                    WordFeatureExtractor = new WordBagEstimator.Options { NgramLength = 2, UseAllLengths = true },
                    CharFeatureExtractor = new WordBagEstimator.Options { NgramLength = 5, UseAllLengths = true },
                },
                nameof(TransactionFeatures.Description)))

            .Append(ml.Transforms.Text.FeaturizeText(
                "TypeFeatures", nameof(TransactionFeatures.TransactionType)))

            .Append(ml.Transforms.NormalizeMeanVariance(
                "AmountFeature", nameof(TransactionFeatures.Amount)))

            .Append(ml.Transforms.Concatenate(
                "Features", "DescriptionFeatures", "TypeFeatures", "AmountFeature"))

            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                labelColumnName: "Label", featureColumnName: "Features"))

            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedCategory", "PredictedLabel"));

        var model = pipeline.Fit(split.TrainSet);

        var metrics = ml.MulticlassClassification.Evaluate(
            model.Transform(split.TestSet), labelColumnName: "Label");

        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        ml.Model.Save(model, all.Schema, modelPath);

        var rows = ml.Data.CreateEnumerable<TransactionFeatures>(all, reuseRowObject: false).ToList();

        return new TrainingReportResponseDto(
            rows.Count,
            rows.Select(r => r.Category).Distinct().Count(),
            metrics.MicroAccuracy,
            metrics.MacroAccuracy);
    }
}
