using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Kasuje paragony razem z transakcjami, do których były przypięte: plik z magazynu i wiersz z bazy, naprawdę.</summary>
/// <remarks>
/// <para>
/// ⚠️ Paragon łączy się z transakcją samą kolumną <c>TransactionBusinessId</c> (bez relacji EF), więc żadne kasowanie transakcji
/// nie dotyka go samo. Paragon bez istniejącej transakcji nie ma żadnego ekranu — użytkownik nie mógłby go usunąć, a wiersz niesie
/// sprzedawcę, produkty i nazwę pliku. Każda ścieżka, która usuwa transakcje (lista, reset, usunięcie i purge budżetu), woła więc tę klasę.
/// </para>
/// <para>
/// Filtr soft delete jest wyłączany jawnie: paragon po transakcji skasowanej wcześniej (przed wprowadzeniem kaskady) też ma zniknąć.
/// Filtr właściciela zostaje, więc zwykły kontekst widzi wyłącznie paragony własnego użytkownika.
/// </para>
/// <para>Pliki idą pierwsze: gdy kasowanie z magazynu się nie powiedzie, wiersz zostaje i kolejne usunięcie ponawia próbę.</para>
/// </remarks>
public sealed class ReceiptCleaner(AppDbContext db, ReceiptFiles files)
{
    /// <returns>Liczba usuniętych paragonów.</returns>
    public async Task<int> DeleteForTransactionsAsync(IReadOnlyCollection<Guid> transactionBusinessIds, CancellationToken ct)
    {
        if (transactionBusinessIds.Count == 0) return 0;

        return await DeleteAsync(
            db.Receipts.IgnoreQueryFilters([QueryFilterNames.SoftDelete])
                .Where(r => r.TransactionBusinessId != null && transactionBusinessIds.Contains(r.TransactionBusinessId.Value)),
            ct);
    }

    /// <returns>Liczba usuniętych paragonów.</returns>
    /// <remarks>Obejmuje też transakcje już skasowane logicznie, bo paragon po nich nie ma innej drogi do usunięcia.</remarks>
    public async Task<int> DeleteForBudgetsAsync(IReadOnlyCollection<Guid> budgetBusinessIds, CancellationToken ct)
    {
        if (budgetBusinessIds.Count == 0) return 0;

        var transactions = db.Transactions.IgnoreQueryFilters([QueryFilterNames.SoftDelete]);

        return await DeleteAsync(
            db.Receipts.IgnoreQueryFilters([QueryFilterNames.SoftDelete])
                .Where(r => transactions.Any(t => t.BusinessId == r.TransactionBusinessId
                    && t.BudgetBusinessId != null && budgetBusinessIds.Contains(t.BudgetBusinessId.Value))),
            ct);
    }

    private async Task<int> DeleteAsync(IQueryable<Domain.Receipt> receipts, CancellationToken ct)
    {
        var doomed = await receipts.Select(r => new { r.BusinessId, r.UserId, r.BlobName }).ToListAsync(ct);
        if (doomed.Count == 0) return 0;

        foreach (var receipt in doomed) await files.DeleteAsync(receipt.UserId, receipt.BlobName, ct);

        var ids = doomed.Select(r => r.BusinessId).ToList();
        return await db.Receipts.IgnoreQueryFilters([QueryFilterNames.SoftDelete])
            .Where(r => ids.Contains(r.BusinessId))
            .ExecuteDeleteAsync(ct);
    }
}
