using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Queries;

/// <summary>Ekran „Dane treningowe": skład zbioru, rozkład po kategoriach, kolejka przeglądu i wersje modelu.</summary>
public sealed class GetTrainingSetQueryHandler(AppDbContext db, TrainingSetBuilder builder, ModelStore store)
{
    /// <summary>Stan zbioru treningowego i modeli na teraz.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>JEDEN odczyt katalogu modeli na żądanie (<see cref="ModelStore.ReadCatalog"/>): lista wersji i trening
    /// aktywnego modelu naraz. Rozdzielone metody hashowały aktywny plik i przeglądały katalog wersji dwa razy.</item>
    /// <item>Bez historii treningu punktem odniesienia dla „przybyło poprawek" jest PLIK BAZOWY — to na nim uczył
    /// się model, który dziś działa. „Nowe" są więc dokładnie te poprawki, których w pliku nie ma; liczenie wszystkich
    /// kwalifikujących się obiecywałoby materiał, który model już widział. Przeetykietowane liczą się TAK SAMO jak
    /// nowe: model ich nie widział w tej postaci, choć same wiersze były w zbiorze od początku.</item>
    /// <item>Z historią liczone bez zapytania — daty przyszły razem ze zbiorem (<see cref="TrainingSet.CorrectionDates"/>).</item>
    /// </list>
    /// </remarks>
    public async Task<TrainingSetOverviewResponseDto> HandleAsync(CancellationToken ct)
    {
        var set = await builder.BuildAsync(ct);

        var pendingReview = await db.Transactions
            .CountAsync(t => t.Status == TransactionStatus.PendingReview, ct);

        var catalog = store.ReadCatalog();

        var sinceLast = catalog.ActiveTraining is not { } last
            ? set.Composition.FromCorrections + set.Composition.Corrected
            : set.CorrectionsNewerThan(last.TrainedAt);

        return new TrainingSetOverviewResponseDto(
            set.Composition,
            CountByCategory(set, await db.Categories.Select(c => c.Name).ToListAsync(ct)),
            pendingReview,
            sinceLast,
            catalog.Versions);
    }

    /// <summary>
    /// Rozkład przykładów po kategoriach, uzupełniony o kategorie z ZEREM przykładów.
    /// </summary>
    /// <remarks>
    /// Te ostatnie są tu najważniejsze i dlatego lista idzie z bazy, a nie ze zbioru:
    /// kategoria, której model nigdy nie widział, nigdy też jej nie wskaże — i nie da się
    /// tego zobaczyć, patrząc wyłącznie na to, co w zbiorze JEST.
    ///
    /// Nazwy ze zbioru, których nie ma już w bazie (kategoria skasowana po treningu), też muszą być widoczne —
    /// inaczej sumy na ekranie nie zgadzałyby się ze składem.
    /// </remarks>
    private static IReadOnlyList<CategoryExampleCountResponseDto> CountByCategory(
        TrainingSet set, IReadOnlyList<string> allCategories)
    {
        var counts = set.Rows
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var names = allCategories
            .Concat(counts.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return [.. names
            .Select(name => new CategoryExampleCountResponseDto(name, counts.GetValueOrDefault(name)))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name, StringComparer.CurrentCulture)];
    }
}
