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
    private readonly string _modelPath;
    private PredictionEngine<TransactionFeatures, CategoryPrediction>? _engine;
    private Dictionary<string, int>? _categoryIds;

    /// <summary>Indeks pierwszego slotu bloku słownego; -1 = nie udało się go znaleźć.</summary>
    private int _wordSlotStart = -1;

    /// <summary>
    /// <c>PredictionEngine</c> nie jest bezpieczny wątkowo, a import leci sekwencyjnie —
    /// blokada jest tania i zdejmuje całą klasę trudnych do odtworzenia błędów.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MlCategorizer(AppDbContext db, IOptions<CategorizationOptions> options)
    {
        _db = db;
        _modelPath = options.Value.ModelPath;
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
    /// Czy opis zapalił CHOĆ JEDNĄ cechę słowną, czyli czy model widział kiedykolwiek
    /// którekolwiek z jego słów.
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
    /// Gdy bloku słownego nie da się wskazać (patrz <see cref="WordSlotPrefix"/>), bramka nie blokuje
    /// niczego. Jest siatką bezpieczeństwa, a nie warunkiem poprawności: jej brak przywraca zachowanie
    /// sprzed jej wprowadzenia, zamiast zatrzymywać kategoryzację.
    /// </remarks>
    private bool DescriptionTouchesVocabulary(in VBuffer<float> features)
    {
        if (_wordSlotStart < 0) return true;

        var values = features.GetValues();

        if (features.IsDense)
        {
            for (var i = _wordSlotStart; i < values.Length; i++)
                if (values[i] != 0) return true;

            return false;
        }

        var indices = features.GetIndices();
        for (var i = 0; i < indices.Length; i++)
            if (indices[i] >= _wordSlotStart && values[i] != 0) return true;

        return false;
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
