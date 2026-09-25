using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Kategoryzacja modelem ML.NET, in-process (CLAUDE.md §3 — bez osobnego mikroserwisu).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Gdy modelu nie ma, zwraca brak zamiast rzucać: aplikacja ma działać na samych regułach, dopóki
/// ktoś nie uruchomi treningu. Przy 97% pokrycia reguł to sensowny stan pośredni, a nowy użytkownik jest
/// w nim zawsze.</item>
/// <item>Klasa trzyma WYŁĄCZNIE stan żądania: silnik predykcji i mapę nazw kategorii. Bajty modelu
/// (<see cref="CategoryModelSource"/>) i policzona bramka (<see cref="VocabularyGate"/>) żyją na wersję
/// modelu i są wspólne dla żądań — wcześniej wszystko siedziało tutaj i mieszały się trzy cykle życia.</item>
/// <item>⚠️ Silnik jest PRYWATNY dla żądania i dlatego nie potrzebuje blokady. <c>PredictionEngine</c> nie
/// jest bezpieczny wątkowo, więc gdyby kiedyś trafił do pamięci współdzielonej albo gdyby pętla importu
/// poszła równolegle, blokada musi wrócić.</item>
/// </list>
/// </remarks>
public sealed class MlCategorizer(AppDbContext db, CategoryModelSource models, VocabularyGate gate)
    : ICategorizer, IDisposable
{
    private PredictionEngine<TransactionFeatures, CategoryPrediction>? _engine;
    private VocabularyGateSlots _slots = VocabularyGateSlots.Open;
    private Dictionary<string, int>? _categoryIds;

    /// <summary>Czy próbowaliśmy już wczytać model — brak modelu też jest odpowiedzią, i to trwałą.</summary>
    private bool _loadAttempted;

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ BRAMKA SŁOWNIKA MUSI BYĆ PRZED PROGIEM PEWNOŚCI, bo próg tego nie łapie.
    ///
    /// Model ma trzy grupy cech: opis, typ operacji i kwotę. Gdy opis nie niesie NIC, co model zna,
    /// decyzję podejmują dwie pozostałe — a wtedy „pewność" mierzy skos klas w obrębie typu operacji,
    /// nie wiedzę o sprzedawcy. Na realnych danych „Płatność kartą" to w zbiorze w większości Subskrypcje,
    /// więc DOWOLNY nieznany opis z tym typem dostawał „Subskrypcje" z pewnością 0,79–0,93 — powyżej progu
    /// 0,7, czyli wchodził automatycznie i nigdy nie trafiał do przeglądu. Losowe ciągi znaków dostawały
    /// wyższą pewność niż realna apteka.
    ///
    /// To ten sam mechanizm, który CLAUDE.md §3 opisuje przy wpływach (wysoka pewność = źle zadane
    /// pytanie), tylko że tam dało się postawić bramkę na znaku kwoty.
    ///
    /// Model znający klasę, której nie ma już w bazie (kategoria usunięta po treningu), zwraca brak:
    /// lepiej wysłać transakcję do przeglądu niż wskazać nieistniejącą kategorię.
    /// </remarks>
    public async Task<CategorySuggestion> CategorizeAsync(
        string normalizedDescription, string transactionType, decimal amount, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        if (_engine is null || _categoryIds is null) return CategorySuggestion.None;

        var prediction = _engine.Predict(new TransactionFeatures
        {
            Description = normalizedDescription,
            TransactionType = transactionType,
            Amount = (float)amount,
        });

        if (!_slots.Allows(prediction.DescriptionFeatures)) return CategorySuggestion.None;

        if (!_categoryIds.TryGetValue(prediction.Category, out var categoryId)) return CategorySuggestion.None;

        var scores = prediction.Score;
        var confidence = scores.Length == 0 ? 0f : scores.Max();
        return new CategorySuggestion(categoryId, (decimal)confidence);
    }

    /// <summary>Silnik, bramka i mapowanie nazw kategorii — raz na żądanie.</summary>
    /// <remarks>
    /// Model operuje NAZWAMI kategorii, baza identyfikatorami, więc mapowanie musi powstać z aktualnego
    /// stanu bazy, a nie z czasu treningu.
    /// </remarks>
    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loadAttempted) return;
        _loadAttempted = true;

        if (await models.ActiveAsync(ct) is not { } active) return;

        _engine = active.Ml.Model.CreatePredictionEngine<TransactionFeatures, CategoryPrediction>(active.Model);
        _slots = await gate.ForAsync(_engine, active.ETag, ct);
        _categoryIds = await db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);
    }

    public void Dispose() => _engine?.Dispose();
}
