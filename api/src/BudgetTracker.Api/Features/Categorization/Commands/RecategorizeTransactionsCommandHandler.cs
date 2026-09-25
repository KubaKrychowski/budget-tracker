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
    
    public async Task<RecategorizeReportResponseDto> HandleAsync(CancellationToken ct)
    {
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
