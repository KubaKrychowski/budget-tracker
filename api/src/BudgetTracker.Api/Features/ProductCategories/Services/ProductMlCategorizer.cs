using BudgetTracker.Api.Features.ProductCategories.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.ML;
using Microsoft.ML.Transforms.Text;

namespace BudgetTracker.Api.Features.ProductCategories.Services;

/// <summary>Kategoryzacja produktów modelem ML.NET uczonym na poprawkach użytkownika — w procesie, bez osobnej wersji ani bloba.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Zbiór treningowy to pozycje paragonów, którym kategorię wybrał UŻYTKOWNIK (<c>CategoryConfirmed</c>). Kategoria z reguły albo
/// z samego modelu nie uczy modelu — inaczej powtarzałby własne pomyłki z rosnącą pewnością.</item>
/// <item>Brak modelu (za mało przykładów: poniżej <see cref="MinExamples"/> albo jedna klasa) to brak podpowiedzi, nie błąd. Na starcie
/// działają same reguły, a model włącza się po zebraniu poprawek — dokładnie jak przy transakcjach.</item>
/// <item>⚠️ Wytrenowany transformer żyje w pamięci procesu pod kluczem z odciskiem zbioru: zmiana kategorii jednej pozycji zmienia odcisk i
/// następne wgranie paragonu uczy model od nowa (ułamki sekundy przy setkach krótkich nazw). Silnik predykcji jest PRYWATNY dla instancji
/// (<c>PredictionEngine</c> nie jest bezpieczny wątkowo).</item>
/// <item>Bramka słownika jak w <c>MlCategorizer</c>: nazwa, z której model nie zna żadnego fragmentu, nie dostaje kategorii, choćby
/// rozkład prawdopodobieństw był „pewny”.</item>
/// </list>
/// </remarks>
public sealed class ProductMlCategorizer(AppDbContext db, ICurrentUserAccessor currentUser, IMemoryCache cache) : IDisposable
{
    /// <summary>Najmniej przykładów potwierdzonych przez użytkownika, od których model w ogóle powstaje.</summary>
    public const int MinExamples = 20;

    private static readonly MLContext Ml = new(seed: 20261014);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    private PredictionEngine<ProductFeatures, ProductPrediction>? _engine;
    private bool _loadAttempted;

    public async Task<ProductSuggestion> CategorizeAsync(string normalizedName, CancellationToken ct)
    {
        if (normalizedName.Length == 0) return ProductSuggestion.None;

        await EnsureLoadedAsync(ct);
        if (_engine is null) return ProductSuggestion.None;

        var prediction = _engine.Predict(new ProductFeatures { Name = normalizedName });
        if (prediction.NameFeatures.GetValues().Length == 0) return ProductSuggestion.None;
        if (!Guid.TryParse(prediction.Category, out var categoryId)) return ProductSuggestion.None;

        var confidence = prediction.Score.Length == 0 ? 0f : prediction.Score.Max();
        return new ProductSuggestion(categoryId, (decimal)confidence);
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loadAttempted) return;
        _loadAttempted = true;

        var examples = await LoadExamplesAsync(ct);
        if (examples.Count < MinExamples || examples.Select(e => e.Category).Distinct().Count() < 2) return;

        var fingerprint = new HashCode();
        foreach (var e in examples)
        {
            fingerprint.Add(e.Name);
            fingerprint.Add(e.Category);
        }

        var key = $"product-model:{currentUser.UserId}:{fingerprint.ToHashCode()}";
        if (!cache.TryGetValue(key, out ITransformer? model) || model is null)
        {
            model = Train(examples);
            cache.Set(key, model, new MemoryCacheEntryOptions { SlidingExpiration = CacheLifetime });
        }

        _engine = Ml.Model.CreatePredictionEngine<ProductFeatures, ProductPrediction>(model);
    }

    private async Task<List<ProductFeatures>> LoadExamplesAsync(CancellationToken ct)
    {
        var receipts = await db.Receipts.AsNoTracking().ToListAsync(ct);
        return [.. receipts
            .SelectMany(r => r.Items)
            .Where(i => i is { ProductCategoryId: not null, CategoryConfirmed: true })
            .Select(i => new ProductFeatures
            {
                Name = ProductNameNormalizer.Normalize(i.Name),
                Category = i.ProductCategoryId!.Value.ToString(),
            })
            .Where(f => f.Name.Length > 0)
            .OrderBy(f => f.Name, StringComparer.Ordinal).ThenBy(f => f.Category, StringComparer.Ordinal)];
    }

    /// <summary>Cechy: n-gramy słów i znaków z nazwy (odporność na literówki i odmianę), klasyfikator liniowy.</summary>
    private static ITransformer Train(List<ProductFeatures> examples)
    {
        var pipeline = Ml.Transforms.Conversion.MapValueToKey("Label", nameof(ProductFeatures.Category))
            .Append(Ml.Transforms.Text.FeaturizeText(
                "NameFeatures",
                new TextFeaturizingEstimator.Options
                {
                    WordFeatureExtractor = new WordBagEstimator.Options { NgramLength = 2, UseAllLengths = true },
                    CharFeatureExtractor = new WordBagEstimator.Options { NgramLength = 4, UseAllLengths = true },
                },
                nameof(ProductFeatures.Name)))
            .Append(Ml.Transforms.CopyColumns("Features", "NameFeatures"))
            .Append(Ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
            .Append(Ml.Transforms.Conversion.MapKeyToValue("PredictedCategory", "PredictedLabel"));

        return pipeline.Fit(Ml.Data.LoadFromEnumerable(examples));
    }

    public void Dispose() => _engine?.Dispose();
}
