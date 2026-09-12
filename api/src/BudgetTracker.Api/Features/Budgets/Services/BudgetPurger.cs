using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>
/// Fizyczne usuwanie skasowanych budżetów razem z dziećmi — mechanika wspólna dla sprzątania
/// po oknie retencji i dla sprzątania wymuszonego.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ To jedyne miejsce w aplikacji, w którym dane znikają fizycznie. <c>ExecuteDelete</c> omija
/// <c>SoftDeleteInterceptor</c> (ten działa na <c>SaveChanges</c>), a dzieci idą przed rodzicem,
/// bo klucze obce są <c>Restrict</c> (CLAUDE.md §4).
/// </para>
/// <para>
/// Warunek „budżet JEST oznaczony jako skasowany" (<c>DeletedAt != null</c>) siedzi tutaj, a nie
/// w wywołujących. Handlery różnią się wyłącznie progiem czasu, więc gdyby każdy budował własne
/// zapytanie, to właśnie ten warunek dałoby się pominąć — a jego pominięcie kasuje żywe budżety
/// bez ostrzeżenia. Jeden warunek w jednym miejscu jest tu zabezpieczeniem, nie oszczędnością linii.
/// </para>
/// </remarks>
public sealed class BudgetPurger(AppDbContext db)
{
    /// <param name="deletedBefore">
    /// Górna granica znacznika usunięcia: kasujemy budżety ostemplowane nie później niż wtedy.
    /// <c>null</c> znaczy BRAK granicy — wszystkie oznaczone jako skasowane, niezależnie od retencji.
    /// </param>
    /// <returns>Liczba trwale usuniętych budżetów.</returns>
    /// <remarks>
    /// Idempotentne: drugi przebieg na tym samym zbiorze nie ma już czego usunąć i zwraca zero.
    /// Całość leci w jednej transakcji — częściowo usunięty budżet (np. bez transakcji, ale z wierszem
    /// budżetu) byłby stanem, którego nie opisuje żaden ekran.
    /// </remarks>
    public async Task<int> PurgeAsync(DateTimeOffset? deletedBefore, CancellationToken ct)
    {
        var doomed = await db.Budgets
            .IgnoreQueryFilters()
            .Where(b => b.DeletedAt != null)
            .Where(b => deletedBefore == null || b.DeletedAt <= deletedBefore)
            .Select(b => new { b.Id, b.BusinessId })
            .ToListAsync(ct);

        if (doomed.Count == 0) return 0;

        var ids = doomed.Select(b => b.Id).ToList();
        var businessIds = doomed.Select(b => b.BusinessId).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Transactions.IgnoreQueryFilters()
            .Where(t => t.BudgetBusinessId != null && businessIds.Contains(t.BudgetBusinessId.Value))
            .ExecuteDeleteAsync(ct);

        await db.ImportBatches.IgnoreQueryFilters()
            .Where(i => ids.Contains(i.BudgetId))
            .ExecuteDeleteAsync(ct);

        await db.BudgetItems.IgnoreQueryFilters()
            .Where(i => businessIds.Contains(i.BudgetBusinessId))
            .ExecuteDeleteAsync(ct);

        await db.Budgets.IgnoreQueryFilters()
            .Where(b => ids.Contains(b.Id))
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);

        return doomed.Count;
    }
}
