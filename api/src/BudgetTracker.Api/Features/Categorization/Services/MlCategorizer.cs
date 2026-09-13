using System.Collections.Concurrent;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Kategoryzacja modelem ML.NET, in-process (CLAUDE.md §3 — bez osobnego mikroserwisu).
/// </summary>
/// <remarks>
/// Gdy modelu nie ma, zwraca brak zamiast rzucać: aplikacja ma działać na samych regułach,
/// dopóki ktoś nie uruchomi treningu. Przy 97% pokrycia reguł to sensowny stan pośredni.
/// </remarks>
public sealed class MlCategorizer : ICategorizer, IDisposable
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

    private readonly MLContext _ml = new();
    private readonly AppDbContext _db;
    private readonly TrainingSetBuilder _trainingSet;
    private readonly string _modelPath;
    private readonly string _trainingDataPath;
    private readonly int _gateMaxCategories;
    private PredictionEngine<TransactionFeatures, CategoryPrediction>? _engine;
    private Dictionary<string, int>? _categoryIds;

    /// <summary>Indeks pierwszego slotu bloku słownego; -1 = nie udało się go znaleźć.</summary>
    private int _wordSlotStart = -1;

    /// <summary>
    /// Sloty słowne, które MOGĄ otworzyć bramkę słownika. <c>null</c> = brak danych treningowych, więc
    /// bramka liczy każde znane słowo, tak jak przed tą zmianą.
    /// </summary>
    /// <remarks>Patrz <see cref="InformativeWordSlotsAsync"/> — dlaczego nie każde znane słowo się liczy.</remarks>
    private bool[]? _informativeWordSlots;

    /// <summary>
    /// Wynik <see cref="InformativeWordSlotsAsync"/> na wersję modelu i pliku zbioru.
    /// </summary>
    /// <remarks>
    /// Statycznie, bo kategoryzator jest <c>Scoped</c> i ładuje model przy każdym żądaniu, a policzenie
    /// rozlania słów to przejście przez cały zbiór treningowy. Klucz niesie ścieżki, czasy zapisu
    /// i rozmiary obu plików, więc nowy model (trening, przywrócenie wersji) liczy się od nowa.
    /// Poprawki z bazy NIE są w kluczu: zmieniają rozlanie słów minimalnie, a ich śledzenie
    /// kosztowałoby zapytanie przy każdym żądaniu.
    /// </remarks>
    private static readonly ConcurrentDictionary<string, bool[]?> InformativeSlotsCache = new();

    /// <summary>
    /// <c>PredictionEngine</c> nie jest bezpieczny wątkowo, a import leci sekwencyjnie —
    /// blokada jest tania i zdejmuje całą klasę trudnych do odtworzenia błędów.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MlCategorizer(AppDbContext db, IOptions<CategorizationOptions> options, TrainingSetBuilder trainingSet)
    {
        _db = db;
        _trainingSet = trainingSet;
        _modelPath = options.Value.ModelPath;
        _trainingDataPath = options.Value.TrainingDataPath;
        _gateMaxCategories = options.Value.VocabularyGateMaxCategories;
    }

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ BRAMKA SŁOWNIKA (<see cref="DescriptionTouchesVocabulary"/>) MUSI BYĆ PRZED PROGIEM PEWNOŚCI,
    /// bo próg tego nie łapie.
    ///
    /// Model ma trzy grupy cech: opis, typ operacji i kwotę. Gdy opis nie niesie NIC, co
    /// model zna, decyzję podejmują dwie pozostałe — a wtedy „pewność" mierzy skos klas
    /// w obrębie typu operacji, nie wiedzę o sprzedawcy. Na realnych danych „Płatność kartą" to w zbiorze
    /// w większości Subskrypcje, więc DOWOLNY nieznany opis z tym typem dostawał „Subskrypcje" z pewnością
    /// 0,79–0,93 — powyżej progu 0,7, czyli wchodził automatycznie i nigdy nie trafiał do przeglądu.
    /// Losowe ciągi znaków dostawały wyższą pewność niż realna apteka.
    ///
    /// To ten sam mechanizm, który CLAUDE.md §3 opisuje przy wpływach (wysoka pewność
    /// = źle zadane pytanie), tylko że tam dało się postawić bramkę na znaku kwoty. Tu nie
    /// ma cechy, po której z góry poznasz nieznany opis — trzeba spytać samego modelu.
    ///
    /// Model znający klasę, której nie ma już w bazie (kategoria usunięta po treningu), zwraca brak:
    /// lepiej wysłać transakcję do przeglądu niż wskazać nieistniejącą kategorię.
    /// </remarks>
    public async Task<CategorySuggestion> CategorizeAsync(
        string normalizedDescription, string transactionType, decimal amount, CancellationToken ct)
    {
        if (!File.Exists(_modelPath)) return CategorySuggestion.None;

        await EnsureLoadedAsync(ct);
        if (_engine is null || _categoryIds is null) return CategorySuggestion.None;

        CategoryPrediction prediction;
        await _lock.WaitAsync(ct);
        try
        {
            prediction = _engine.Predict(new TransactionFeatures
            {
                Description = normalizedDescription,
                TransactionType = transactionType,
                Amount = (float)amount,
            });
        }
        finally
        {
            _lock.Release();
        }

        if (!DescriptionTouchesVocabulary(prediction.DescriptionFeatures)) return CategorySuggestion.None;

        if (!_categoryIds.TryGetValue(prediction.Category, out var categoryId))
        {
            return CategorySuggestion.None;
        }

        var scores = prediction.Score;
        var confidence = scores.Length == 0 ? 0f : scores.Max();
        return new CategorySuggestion(categoryId, (decimal)confidence);
    }

    /// <summary>
    /// Czy opis zapalił CHOĆ JEDNĄ INFORMATYWNĄ cechę słowną, czyli czy model zna z niego słowo,
    /// które coś mówi o sprzedawcy.
    /// </summary>
    /// <remarks>
    /// Celowo blok słowny, nie znakowy: char-gramy zapalają się prawie zawsze, bo trójka liter
    /// w rodzaju „ent" trafia się w czymkolwiek — na losowym ciągu też. Blok słowny jest
    /// zero-jedynkowy i dlatego nie ma tu progu do strojenia: albo model zna jakieś słowo
    /// z tego opisu, albo nie zna żadnego.
    ///
    /// Nie zastępuje progu pewności, tylko domyka jego dziurę. Opis z częściową wiedzą
    /// („nowa piekarnia u zosi" — dwa znane słowa) przechodzi przez bramkę i dostaje pewność
    /// 0,33, więc i tak idzie do przeglądu. Bramka odpowiada wyłącznie za przypadek,
    /// w którym opis nie mówi modelowi ZUPEŁNIE nic.
    ///
    /// ⚠️ „Znane słowo" to za mało (zgłoszenie #9, druga odsłona). Parser PKO dokleja do każdej płatności
    /// kartą „Miasto: …", a „miasto" zapalało się w zbiorze w 19 z 25 kategorii. Taka bramka przepuszczała
    /// więc KAŻDĄ płatność kartą, także losowy ciąg znaków — i model znów dawał „Subskrypcje" z pewnością
    /// 0,72–0,81. Dlatego liczą się tylko sloty z <see cref="_informativeWordSlots"/>.
    ///
    /// Gdy bloku słownego nie da się wskazać (patrz <see cref="WordSlotPrefix"/>), bramka nie blokuje
    /// niczego. Jest siatką bezpieczeństwa, a nie warunkiem poprawności: jej brak przywraca zachowanie
    /// sprzed jej wprowadzenia, zamiast zatrzymywać kategoryzację.
    /// </remarks>
    private bool DescriptionTouchesVocabulary(in VBuffer<float> features)
    {
        if (_wordSlotStart < 0) return true;

        foreach (var slot in LitWordSlots(features, _wordSlotStart))
        {
            if (_informativeWordSlots is null || slot >= _informativeWordSlots.Length || _informativeWordSlots[slot])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Indeksy zapalonych slotów bloku słownego.</summary>
    private static IEnumerable<int> LitWordSlots(VBuffer<float> features, int wordSlotStart)
    {
        var values = features.GetValues().ToArray();

        if (features.IsDense)
        {
            for (var i = wordSlotStart; i < values.Length; i++)
                if (values[i] != 0) yield return i;

            yield break;
        }

        var indices = features.GetIndices().ToArray();
        for (var i = 0; i < indices.Length; i++)
            if (indices[i] >= wordSlotStart && values[i] != 0) yield return indices[i];
    }

    /// <summary>
    /// Które sloty słowne mogą otworzyć bramkę: wszystkie POZA tymi, które w zbiorze treningowym zapalają się
    /// w więcej niż <see cref="CategorizationOptions.VocabularyGateMaxCategories"/> kategoriach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Słowo obecne w wielu kategoriach nie mówi nic o sprzedawcy — jest etykietą z wyciągu („miasto")
    /// albo nazwą miasta, w którym robi się prawie wszystkie zakupy. Sprzedawca zapala się zwykle w jednej
    /// kategorii: w zbiorze 1242 z 1414 zapalonych słów występuje w dokładnie jednej.
    /// </para>
    /// <para>
    /// ⚠️ Rozlanie liczone jest PRZEZ SAM MODEL — wiersze zbioru idą przez załadowany potok i czytamy,
    /// które sloty się zapalają. Własna tokenizacja rozjechałaby się z <c>FeaturizeText</c> (bigramy,
    /// normalizacja) i bramka pytałaby o inne słowa niż te, które widzi model.
    /// </para>
    /// <para>
    /// Liczone przy wczytaniu modelu, a nie przy treningu: działa od razu na istniejącym modelu, bez
    /// douczania, i zawsze pasuje do tej wersji, która jest załadowana — także po przywróceniu starszej.
    /// Wynik trzymany jest w <see cref="InformativeSlotsCache"/>, więc koszt płaci się raz na model.
    /// </para>
    /// <para>
    /// Wykluczane są wyłącznie sloty z UDOWODNIONYM rozlaniem. Slot, którego zbiór nie zapalił (np. plik
    /// zmienił się po treningu), liczy się jak dotąd — bramka ma odcinać szum, a nie po cichu wysyłać
    /// do przeglądu wszystko, czego akurat nie potrafi policzyć. Bez danych treningowych zwraca
    /// <c>null</c> i bramka wraca do „dowolne znane słowo".
    /// </para>
    /// </remarks>
    private async Task<bool[]?> InformativeWordSlotsAsync(CancellationToken ct)
    {
        if (_engine is null || _wordSlotStart < 0) return null;

        var set = await _trainingSet.BuildAsync(ct);
        if (set.Rows.Count == 0) return null;

        var slotCount = (_engine.OutputSchema[DescriptionFeaturesColumn].Type as VectorDataViewType)?.Size ?? 0;
        if (slotCount == 0) return null;

        var categoriesPerSlot = new Dictionary<int, HashSet<string>>();
        foreach (var row in set.Rows)
        {
            var prediction = _engine.Predict(new TransactionFeatures
            {
                Description = DescriptionNormalizer.Normalize(row.Description),
                TransactionType = row.TransactionType,
                Amount = row.Amount,
            });

            foreach (var slot in LitWordSlots(prediction.DescriptionFeatures, _wordSlotStart))
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
            if (slot < slotCount && categories.Count > _gateMaxCategories) informative[slot] = false;
        }

        return informative;
    }

    /// <summary>Klucz <see cref="InformativeSlotsCache"/>: tożsamość pliku modelu, pliku zbioru i progu.</summary>
    private string InformativeSlotsCacheKey()
    {
        static string Identity(string path) => File.Exists(path)
            ? $"{Path.GetFullPath(path)}|{File.GetLastWriteTimeUtc(path).Ticks}|{new FileInfo(path).Length}"
            : $"{path}|brak";

        return $"{Identity(_modelPath)}#{Identity(_trainingDataPath)}#{_gateMaxCategories}";
    }

    /// <summary>Wczytuje model i mapowanie nazw kategorii na klucze — raz na instancję.</summary>
    /// <remarks>
    /// Model operuje NAZWAMI kategorii, baza identyfikatorami — mapowanie musi powstać z aktualnego
    /// stanu bazy, nie z czasu treningu.
    /// </remarks>
    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_engine is not null && _categoryIds is not null) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_engine is null)
            {
                using var stream = File.OpenRead(_modelPath);
                var model = _ml.Model.Load(stream, out _);
                _engine = _ml.Model.CreatePredictionEngine<TransactionFeatures, CategoryPrediction>(model);
                _wordSlotStart = FindWordSlotStart(_engine.OutputSchema);

                var key = InformativeSlotsCacheKey();
                if (!InformativeSlotsCache.TryGetValue(key, out _informativeWordSlots))
                {
                    _informativeWordSlots = await InformativeWordSlotsAsync(ct);
                    InformativeSlotsCache[key] = _informativeWordSlots;
                }
            }

            _categoryIds ??= await _db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Znajduje, od którego wymiaru wektora cech opisu zaczyna się blok słowny.
    /// </summary>
    /// <remarks>
    /// Granica idzie z NAZW SLOTÓW zapisanych w samym pliku modelu, nie z liczby wyliczonej
    /// przy treningu. Dzięki temu jest zawsze zgodna z załadowaną wersją modelu — także wtedy,
    /// gdy ktoś cofnie się do starszej (ekran „Dane treningowe" na to pozwala), a ta miała
    /// inny rozmiar słownika.
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

    public void Dispose()
    {
        _engine?.Dispose();
        _lock.Dispose();
    }
}
