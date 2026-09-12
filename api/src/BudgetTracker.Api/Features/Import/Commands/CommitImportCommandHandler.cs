using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Import.Commands;

/// <summary>
/// Zapisuje wiersze zaakceptowane przez użytkownika — po jego usunięciach i korektach kategorii z kroku 3.
/// </summary>
/// <remarks>
/// Przyjmuje wiersze, a nie plik: gdyby parsował plik ponownie, wyrzuciłby do kosza wszystko, co użytkownik
/// właśnie poprawił.
///
/// Deduplikacja jest sprawdzana PONOWNIE, mimo że podgląd już ją liczył. Między
/// podglądem a zatwierdzeniem mógł minąć dowolny czas i wejść inny import.
/// </remarks>
public sealed class CommitImportCommandHandler(
    AppDbContext db,
    TimeProvider clock,
    ImportBudgetLookup budgets,
    ExistingTransactionKeys existingTransactionKeys,
    ImportConfidenceThreshold threshold)
{
    /// <summary>Zapisuje import i zwraca liczby na ekran „Podsumowanie".</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Kategorie przychodzą z frontu jako <c>BusinessId</c> — zamiana na klucze zapisu jednym zapytaniem na cały
    /// import, nie jednym na wiersz. Nieznana kategoria = brak kategorii, nie wyjątek: wiersz trafi do przeglądu
    /// zamiast wywracać cały import przez jeden zły identyfikator z frontu.</item>
    /// <item>Kategoria wskazana przez człowieka nie ma „pewności" — to nie predykcja (<see cref="ManualCategoryCorrection"/>).</item>
    /// <item><see cref="ImportBatch"/> zapisuje się pierwszym <c>SaveChanges</c> wewnątrz transakcji bazodanowej:
    /// transakcje odwołują się do niego kluczem, a bez nawigacji EF nie uzupełni go sam. Transakcja bazodanowa pilnuje,
    /// żeby po nieudanym zapisie wierszy nie został pusty ślad importu.</item>
    /// </list>
    /// </remarks>
    public async Task<ImportSummaryResponseDto> HandleAsync(CommitRequestDto request, CancellationToken ct)
    {
        var rows = request.Rows;
        var budget = await budgets.FindAcceptingAsync(request.BudgetId, ct);

        var categoryKeys = await db.Categories
            .Select(c => new { c.Id, c.BusinessId })
            .ToDictionaryAsync(c => c.BusinessId, c => c.Id, ct);

        var existingKeys = await existingTransactionKeys.LoadAsync(
            rows.Select(r => r.ToParsedRow()).ToList(), budget.BusinessId, ct);

        var now = clock.GetUtcNow();
        var batch = new ImportBatch(budget.Id, request.Bank, request.FileName, rows.Count, now);
        db.ImportBatches.Add(batch);

        await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);

        var saved = new List<Transaction>(rows.Count);

        foreach (var row in rows)
        {
            if (existingKeys.Contains(row.ToParsedRow().IdentityKey()))
            {
                batch.CountSkippedDuplicate();
                continue;
            }

            var status = StatusFor(row);
            if (status == TransactionStatus.PendingReview) batch.CountPendingReview();
            else batch.CountImported();

            var transaction = new Transaction(
                row.Date,
                row.Amount,
                row.Description,
                now,
                status,
                categoryId: ResolveCategory(row.CategoryId, categoryKeys),
                confidence: row.Edited ? ManualCategoryCorrection.Confidence : row.Confidence,
                transactionType: row.TransactionType,
                externalReference: row.ExternalReference,
                budgetBusinessId: budget.BusinessId,
                importBatchId: batch.Id);

            db.Transactions.Add(transaction);
            saved.Add(transaction);
        }

        await db.SaveChangesAsync(ct);
        await dbTransaction.CommitAsync(ct);

        return await BuildSummaryAsync(batch, saved, budget, ct);
    }

    /// <summary>
    /// Status wynika z tego, KTO nadał kategorię — dlatego front przysyła <c>edited</c>.
    /// </summary>
    /// <remarks>
    /// Bez tego korekta użytkownika byłaby nieodróżnialna od trafienia modelu i wpadłaby
    /// z powrotem do kolejki „do przeglądu", którą właśnie ręcznie rozbroił.
    ///
    /// ⚠️ Gałąź progu jest tu KONIECZNA, odkąd wiersz może przyjść z kategorią, której model
    /// nie jest pewny. Bez niej podpowiedź z pewnością 0,46 zapisałaby się jako
    /// <see cref="TransactionStatus.AutoCategorized"/> — czyli dokładnie jako coś, czego nikt
    /// nie sprawdził i nikt już nie sprawdzi. Próg liczy SERWER, ponownie, na własnych danych:
    /// flaga z frontu byłaby tu deklaracją klienta o tym, czy ufać jego własnej odpowiedzi.
    /// </remarks>
    private TransactionStatus StatusFor(CommitRowRequestDto row) => row switch
    {
        { CategoryId: null } => TransactionStatus.PendingReview,
        { Edited: true } => ManualCategoryCorrection.Status,
        _ when !threshold.IsConfident(row.Confidence) => TransactionStatus.PendingReview,
        _ => TransactionStatus.AutoCategorized,
    };

    /// <summary>Liczby na ekran „Podsumowanie" (krok 4 makiety).</summary>
    /// <remarks>
    /// „Aktualny stan budżetu" = bilans początkowy plus WSZYSTKIE transakcje tego budżetu.
    /// Ta sama formuła co na dashboardzie (<c>GetDashboardQueryHandler</c>) — inaczej ten sam budżet
    /// pokazywałby dwie różne kwoty na dwóch ekranach. Liczone z bazy, nie z samego
    /// importu, bo budżet mógł mieć już wcześniejsze wpisy.
    /// </remarks>
    private async Task<ImportSummaryResponseDto> BuildSummaryAsync(
        ImportBatch batch, List<Transaction> saved, Budget budget, CancellationToken ct)
    {
        var booked = await db.Transactions
            .Where(t => t.BudgetBusinessId == budget.BusinessId)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        var withConfidence = saved.Where(t => t.Confidence is not null).ToList();

        return new ImportSummaryResponseDto(
            BatchId: batch.BusinessId,
            RowsInFile: batch.RowCount,
            Imported: batch.ImportedCount,
            PendingReview: batch.PendingReviewCount,
            SkippedDuplicates: batch.SkippedDuplicateCount,
            TotalExpenses: -saved.Where(t => t.Amount < 0).Sum(t => t.Amount),
            TotalIncome: saved.Where(t => t.Amount > 0).Sum(t => t.Amount),
            PeriodFrom: saved.Count == 0 ? null : saved.Min(t => t.Date),
            PeriodTo: saved.Count == 0 ? null : saved.Max(t => t.Date),
            AverageConfidence: withConfidence.Count == 0
                ? null
                : withConfidence.Average(t => t.Confidence!.Value),
            BudgetBalance: budget.InitialBalance + booked);
    }

    /// <summary>Publiczny identyfikator kategorii → klucz zapisu; nieznany albo pusty = brak kategorii.</summary>
    private static int? ResolveCategory(Guid? businessId, IReadOnlyDictionary<Guid, int> keys) =>
        businessId is { } id && keys.TryGetValue(id, out var key) ? key : null;
}
