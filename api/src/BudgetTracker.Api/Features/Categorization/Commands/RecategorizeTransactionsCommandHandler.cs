using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>
/// Przelicza kategorie wierszy, które SĄ JUŻ W BAZIE, aktualnym modelem i aktualnymi regułami.
/// </summary>
/// <remarks>
/// Bez tego douczanie nie miało jak zadziałać i nie widać było, żeby cokolwiek dało:
/// <c>ICategorizer</c> odpytywał WYŁĄCZNIE import, więc nowy model dotyczył tylko wierszy
/// jeszcze niezaimportowanych, a ponowne wgranie tego samego wyciągu jest deduplikowane
/// co do wiersza. Użytkownik poprawiał kategorie, trenował, importował ponownie — i widział
/// różnicę rzędu jednego rekordu, bo model nie miał czego dotknąć.
///
/// Świadomie RĘCZNA akcja, nie krok treningu: przepisuje kategorie w danych, na które
/// użytkownik właśnie nie patrzy, więc musi mieć moment, w którym można się nie zgodzić.
/// </remarks>
public sealed class RecategorizeTransactionsCommandHandler(
    AppDbContext db,
    ModelStore store,
    ICategorizer categorizer,
    IOptions<CategorizationOptions> options)
{
    private readonly decimal _threshold = options.Value.ConfidenceThreshold;

    /// <summary>
    /// Stany, w których o kategorii NIE zdecydował człowiek — tylko te wolno przeliczyć.
    /// </summary>
    /// <remarks>
    /// ⚠️ <see cref="TransactionStatus.ManuallyCategorized"/> i <see cref="TransactionStatus.Confirmed"/>
    /// są tu nieobecne CELOWO i nie wolno ich dodać. To są dokładnie te wiersze, na których
    /// model się uczy (<see cref="TrainingSetBuilder"/>); nadpisanie ich predykcją zamknęłoby
    /// pętlę z CLAUDE.md §3 samą na siebie — model kasowałby materiał, z którego powstał,
    /// a użytkownik oglądałby, jak jego własne poprawki znikają po każdym przeliczeniu.
    /// </remarks>
    private static readonly TransactionStatus[] WithoutHumanDecision =
    [
        TransactionStatus.Imported,
        TransactionStatus.AutoCategorized,
        TransactionStatus.PendingReview,
    ];

    /// <summary>Przelicza wiersze bez decyzji człowieka i raportuje, co się zmieniło.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Ta sama blokada co przy imporcie, i z tego samego powodu: przez cały przebieg ma obowiązywać JEDNA
    /// wersja modelu. Bez niej trening opublikowany w trakcie przeliczania rozdzieliłby wynik na „przed" i „po"
    /// — w jednej operacji.</item>
    /// <item>Wyłączone budżety też — <c>DisabledAt</c> zamyka budżet na NOWE dane (CLAUDE.md §5), a to jest
    /// przeliczenie tego, co już w nim jest, nie dopisywanie.</item>
    /// <item>Ta sama droga co w imporcie: normalizacja, potem reguły, potem model. Dzięki temu przeliczenie łapie też
    /// reguły dodane PO imporcie, nie tylko nowy model.</item>
    /// <item>⚠️ Kategorię przepisujemy TAKŻE poniżej progu — ta sama zasada co w imporcie
    /// (<c>CommitImportCommandHandler.StatusFor</c>): próg rozstrzyga STATUS, a nie to, czy podpowiedź w ogóle
    /// zostaje. Wiersz bez kategorii i wiersz z niepewną kategorią wołają o to samo (przegląd), ale ten drugi
    /// wystarczy potwierdzić.</item>
    /// <item>Kategoria ta sama, ale model stracił pewność — wiersz wraca do kolejki, więc licznik przeniesionych
    /// do przeglądu musi to pokazać. Bez tego użytkownik zobaczyłby „0 zmian" nad kolejką, która właśnie urosła.</item>
    /// </list>
    /// </remarks>
    public async Task<RecategorizeReportResponseDto> HandleAsync(CancellationToken ct)
    {
        using var lease = store.BeginImport();

        var rows = await db.Transactions
            .Where(t => WithoutHumanDecision.Contains(t.Status))
            .ToListAsync(ct);

        var recategorized = 0;
        var movedToReview = 0;

        foreach (var transaction in rows)
        {
            var suggestion = await categorizer.CategorizeAsync(
                DescriptionNormalizer.Normalize(transaction.Description),
                transaction.TransactionType,
                transaction.Amount,
                ct);

            var categoryId = suggestion.CategoryId;
            var confident = categoryId is not null && (suggestion.Confidence ?? 0m) >= _threshold;

            var status = confident
                ? TransactionStatus.AutoCategorized
                : TransactionStatus.PendingReview;

            if (categoryId != transaction.CategoryId)
            {
                recategorized++;
                if (categoryId is null) movedToReview++;
            }
            else if (status != transaction.Status && status == TransactionStatus.PendingReview)
            {
                movedToReview++;
            }

            transaction.Recategorize(categoryId, status, suggestion.Confidence);
        }

        await db.SaveChangesAsync(ct);

        return new RecategorizeReportResponseDto(
            rows.Count, recategorized, movedToReview, rows.Count - recategorized);
    }
}
