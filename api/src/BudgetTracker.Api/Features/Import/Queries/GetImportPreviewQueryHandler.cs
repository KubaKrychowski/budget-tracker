using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Models;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Import.Queries;

/// <summary>
/// Krok 3 steppera: co system zrobiłby z wczytanym plikiem. Niczego nie utrwala.
/// </summary>
/// <remarks>
/// Import jest DWUFAZOWY, bo krok 3 pozwala poprawić wynik przed zapisem: ten handler liczy, co by się stało,
/// a <c>CommitImportCommandHandler</c> zapisuje to, co użytkownik ostatecznie zaakceptował.
/// </remarks>
public sealed class GetImportPreviewQueryHandler(
    AppDbContext db,
    ICategorizer categorizer,
    ModelStore modelStore,
    ImportBudgetLookup budgets,
    ExistingTransactionKeys existingTransactionKeys,
    ImportConfidenceThreshold threshold)
{
    /// <summary>Podgląd importu wierszy <paramref name="rows"/> na budżet <paramref name="budgetId"/>.</summary>
    /// <remarks>
    /// KOLEJNOŚĆ KROKÓW JEST ISTOTNA:
    /// <list type="number">
    /// <item>Znacznik „trwa kategoryzacja" obejmuje CAŁY podgląd, nie pojedynczą predykcję: chodzi o spójność jednego
    /// pliku. Trening puszczony w połowie podmieniłby model i druga połowa wyciągu dostałaby kategorie z innego modelu,
    /// bez żadnego śladu. Trening w tym czasie dostaje 409 — patrz <see cref="ModelStore"/>.</item>
    /// <item>Nieznany budżet = 404, nie cicha pustka: podgląd bez budżetu pokazałby zero duplikatów i wpuścił wszystko
    /// drugi raz.</item>
    /// <item>Scalenie duplikatów WEWNĄTRZ pliku — zanim cokolwiek dotknie bazy (<see cref="MergeDuplicates"/>).</item>
    /// <item>Duplikaty względem bazy, w obrębie wskazanego budżetu. Duplikat nie idzie do kategoryzacji i nie idzie
    /// do zapisu, więc nie ma czego przeglądać — inaczej dopisywałby się do licznika „do weryfikacji".</item>
    /// <item>Normalizacja + kategoryzacja. Model dostaje jeden wiersz zamiast trzech identycznych, więc nie ma jak zwrócić
    /// dla nich rozbieżnych kategorii. Kategorię bierzemy ZAWSZE, gdy model albo reguła cokolwiek wskazały — także
    /// poniżej progu. Próg rozstrzyga już tylko STATUS, nie to, czy podpowiedź w ogóle się pokaże (patrz
    /// <see cref="PreviewRowResponseDto"/>). Dopasowanie regułą ma pewność 1.0, więc zawsze przechodzi bez przeglądu.</item>
    /// </list>
    /// ⚠️ Liczniki idą po <c>NeedsReview</c>, a NIE po obecności kategorii. Odkąd niepewna podpowiedź też wchodzi
    /// do wiersza, „ma kategorię" przestało znaczyć „gotowe" — po staremu ekran pokazałby „do weryfikacji: 0"
    /// nad wierszami wołającymi o przegląd.
    /// </remarks>
    public async Task<ImportPreviewResponseDto> HandleAsync(
        IReadOnlyList<ParsedRow> rows, Guid budgetId, CancellationToken ct)
    {
        using var lease = modelStore.BeginImport();

        var budget = await budgets.FindAcceptingAsync(budgetId, ct);
        var merged = MergeDuplicates(rows);
        var existingKeys = await existingTransactionKeys.LoadAsync(merged, budget.BusinessId, ct);

        var previewRows = new List<PreviewRowResponseDto>(merged.Count);
        var categories = await db.Categories
            .Select(c => new { c.Id, c.BusinessId, c.Name })
            .ToDictionaryAsync(c => c.Id, ct);

        foreach (var row in merged)
        {
            var duplicate = existingKeys.Contains(row.IdentityKey());

            int? categoryId = null;
            decimal? confidence = null;
            var needsReview = false;

            if (!duplicate)
            {
                var normalized = DescriptionNormalizer.Normalize(row.Description);
                var suggestion = await categorizer.CategorizeAsync(
                    normalized, row.TransactionType, row.Amount, ct);

                confidence = suggestion.Confidence;
                categoryId = suggestion.CategoryId;
                needsReview = categoryId is null || !threshold.IsConfident(confidence);
            }

            previewRows.Add(new PreviewRowResponseDto(
                Index: previewRows.Count,
                Date: row.Date,
                Amount: row.Amount,
                Description: row.Description,
                TransactionType: row.TransactionType,
                ExternalReference: row.ExternalReference,
                CategoryId: categoryId is { } id ? categories[id].BusinessId : null,
                CategoryName: categoryId is { } named ? categories[named].Name : null,
                Confidence: confidence,
                NeedsReview: needsReview,
                Duplicate: duplicate));
        }

        return new ImportPreviewResponseDto(
            RowsInFile: previewRows.Count,
            WillImport: previewRows.Count(r => !r.Duplicate && !r.NeedsReview),
            PendingReview: previewRows.Count(r => !r.Duplicate && r.NeedsReview),
            SkippedDuplicates: previewRows.Count(r => r.Duplicate),
            Rows: previewRows);
    }

    /// <summary>
    /// Wiersze o tym samym kluczu tożsamości sumujemy w jeden, w kolejności z pliku.
    /// </summary>
    /// <remarks>
    /// Decyzja użytkownika: dla trackera wydatków znaczenie ma kwota, nie liczba przyłożeń
    /// telefonu do czytnika. Trzy bilety po 4,20 zł z jednego dnia pokazujemy jako jedną pozycję 12,60 zł.
    /// Dzięki temu klucz pozostaje unikalny i deduplikacja względem bazy sprowadza się do
    /// „istnieje / nie istnieje", bez liczenia wystąpień. Gdyby ten krok był po porównaniu z bazą,
    /// niescalone wiersze nie pasowałyby do zsumowanych rekordów i każdy import mnożyłby dane.
    ///
    /// Scalanie MUSI być deterministyczne — inaczej ten sam plik dałby raz trzy pozycje,
    /// raz jedną zsumowaną, a klucze przestałyby się zgadzać między importami. Stąd jawna lista kolejności
    /// z pliku: wynik nie zależy od hashowania słownika.
    /// </remarks>
    private static List<ParsedRow> MergeDuplicates(IReadOnlyList<ParsedRow> rows)
    {
        var merged = new Dictionary<string, ParsedRow>();
        var order = new List<string>();

        foreach (var row in rows)
        {
            var key = row.IdentityKey();
            if (merged.TryGetValue(key, out var existing))
            {
                merged[key] = existing with { Amount = existing.Amount + row.Amount };
            }
            else
            {
                merged[key] = row;
                order.Add(key);
            }
        }

        return order.Select(k => merged[k]).ToList();
    }
}
