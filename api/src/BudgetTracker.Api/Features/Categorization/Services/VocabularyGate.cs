using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Liczy, które sloty słowne modelu niosą informację o sprzedawcy, a które są szumem.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Słowo obecne w wielu kategoriach nie mówi nic o sprzedawcy — jest etykietą z wyciągu („miasto")
/// albo nazwą miasta, w którym robi się prawie wszystkie zakupy. Sprzedawca zapala się zwykle w jednej
/// kategorii: w zbiorze 1242 z 1414 zapalonych słów występuje w dokładnie jednej.</item>
/// <item>⚠️ Rozlanie liczy SAM MODEL — wiersze zbioru idą przez załadowany potok i czytamy, które sloty
/// się zapalają. Własna tokenizacja rozjechałaby się z <c>FeaturizeText</c> (bigramy, normalizacja),
/// więc bramka pytałaby o inne słowa niż te, które widzi model.</item>
/// <item>⚠️ Koszt: przejście po CAŁYM zbiorze treningowym z predykcją na każdy wiersz, plus pobranie
/// zbioru. Dlatego wynik jest pamiętany na wersję modelu — bez tego płaciłby to każdy import,
/// dla każdego wiersza z osobna.</item>
/// <item>Liczone przy wczytaniu modelu, a nie przy treningu: działa od razu na istniejącym modelu
/// i zawsze pasuje do wersji, która jest załadowana — także po przywróceniu starszej.</item>
/// </list>
/// </remarks>
public sealed class VocabularyGate(
    TrainingSetBuilder trainingSet,
    IOptions<CategorizationOptions> options,
    ICurrentUserAccessor currentUser,
    IMemoryCache cache)
{
    /// <summary>
    /// Kolumna z wnętrza potoku (patrz <see cref="CategoryModelTrainer"/>) — cechy policzone
    /// z SAMEGO opisu, przed sklejeniem ich z typem operacji i kwotą.
    /// </summary>
    private const string DescriptionFeaturesColumn = "DescriptionFeatures";

    /// <summary>
    /// Prefiks, którym <c>FeaturizeText</c> nazywa sloty bloku słownego. Blok znakowy nosi
    /// <c>Char.</c> i idzie pierwszy, słowny — drugi.
    /// </summary>
    /// <remarks>
    /// ⚠️ To nazwa z wnętrza ML.NET, nie nasza. Pilnuje jej osobny test — gdyby aktualizacja
    /// biblioteki zmieniła nazewnictwo, ma paść test, a nie cicho wyłączyć się bramka słownika.
    /// </remarks>
    private const string WordSlotPrefix = "Word.";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Bramka dla wskazanej wersji modelu — z pamięci podręcznej albo policzona od nowa.</summary>
    /// <remarks>
    /// ⚠️ Klucz niesie wersję MODELU, a nie zbioru treningowego. Poprawki dopisane po treningu zmieniają
    /// rozlanie słów minimalnie, a ich śledzenie kosztowałoby zapytanie przy każdym żądaniu; nowy trening
    /// i tak daje nowy <c>ETag</c>, więc bramka przelicza się razem z modelem, którego dotyczy.
    /// </remarks>
    public async Task<VocabularyGateSlots> ForAsync(
        PredictionEngine<TransactionFeatures, CategoryPrediction> engine, string modelETag, CancellationToken ct)
    {
        var key = $"vocabulary-gate:{currentUser.UserId}:{modelETag}:{options.Value.VocabularyGateMaxCategories}";
        if (cache.TryGetValue(key, out VocabularyGateSlots? cached) && cached is not null) return cached;

        var slots = await ComputeAsync(engine, ct);
        cache.Set(key, slots, new MemoryCacheEntryOptions { SlidingExpiration = CacheLifetime });
        return slots;
    }

    private async Task<VocabularyGateSlots> ComputeAsync(
        PredictionEngine<TransactionFeatures, CategoryPrediction> engine, CancellationToken ct)
    {
        var wordSlotStart = FindWordSlotStart(engine.OutputSchema);
        if (wordSlotStart < 0) return VocabularyGateSlots.Open;

        var set = await trainingSet.BuildAsync(ct);
        if (set.Rows.Count == 0) return new VocabularyGateSlots(wordSlotStart, null);

        var slotCount = (engine.OutputSchema[DescriptionFeaturesColumn].Type as VectorDataViewType)?.Size ?? 0;
        if (slotCount == 0) return new VocabularyGateSlots(wordSlotStart, null);

        var categoriesPerSlot = new Dictionary<int, HashSet<string>>();
        foreach (var row in set.Rows)
        {
            ct.ThrowIfCancellationRequested();

            var prediction = engine.Predict(new TransactionFeatures
            {
                Description = DescriptionNormalizer.Normalize(row.Description),
                TransactionType = row.TransactionType,
                Amount = row.Amount,
            });

            foreach (var slot in VocabularyGateSlots.LitWordSlots(prediction.DescriptionFeatures, wordSlotStart))
            {
                if (!categoriesPerSlot.TryGetValue(slot, out var categories))
                {
                    categoriesPerSlot[slot] = categories = [];
                }

                categories.Add(row.Category);
            }
        }

        var informative = Enumerable.Repeat(true, slotCount).ToArray();
        foreach (var (slot, categories) in categoriesPerSlot)
        {
            if (slot < slotCount && categories.Count > options.Value.VocabularyGateMaxCategories)
            {
                informative[slot] = false;
            }
        }

        return new VocabularyGateSlots(wordSlotStart, informative);
    }

    /// <summary>Od którego wymiaru wektora cech opisu zaczyna się blok słowny.</summary>
    /// <remarks>
    /// Granica idzie z NAZW SLOTÓW zapisanych w samym modelu, nie z liczby wyliczonej przy treningu.
    /// Dzięki temu jest zawsze zgodna z załadowaną wersją — także wtedy, gdy ktoś cofnie się do starszej
    /// (ekran „Dane treningowe" na to pozwala), a ta miała inny rozmiar słownika.
    /// </remarks>
    private static int FindWordSlotStart(DataViewSchema schema)
    {
        if (schema.GetColumnOrNull(DescriptionFeaturesColumn) is not { } column) return -1;
        if (!column.HasSlotNames()) return -1;

        VBuffer<ReadOnlyMemory<char>> slots = default;
        column.GetSlotNames(ref slots);

        var index = 0;
        foreach (var name in slots.DenseValues())
        {
            if (name.Span.StartsWith(WordSlotPrefix, StringComparison.Ordinal)) return index;
            index++;
        }

        return -1;
    }
}
