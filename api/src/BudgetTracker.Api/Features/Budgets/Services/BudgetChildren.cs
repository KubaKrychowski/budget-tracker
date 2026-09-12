using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>
/// „Kaskada" w kodzie, nie w bazie (CLAUDE.md §4): transakcje, wpisy importów i limity budżetu
/// są stemplowane i przywracane razem z nim.
/// </summary>
/// <remarks>
/// Transakcje i limity łączą się z budżetem po <c>BusinessId</c> (bez relacji EF), importy dalej po <c>Id</c>.
/// <c>ExecuteUpdate</c> zamiast wczytywania encji: transakcji bywa ponad tysiąc na budżet, więc
/// materializowanie ich tylko po to, by ustawić jedno pole, byłoby marnotrawstwem.
/// </remarks>
public sealed class BudgetChildren(AppDbContext db)
{
    /// <summary>Stempluje żywe dzieci budżetu znacznikiem <paramref name="deletedAt"/>.</summary>
    /// <remarks>
    /// ⚠️ Znacznik podaje wywołujący, a nie czyta go tu z zegara. Usunięcie stempluje tym samym czasem
    /// budżet i dzieci, a przywracanie rozpoznaje dzieci właśnie po RÓWNOŚCI znaczników — dwa odczyty
    /// zegara dałyby dwa różne czasy i przywrócony budżet wróciłby bez swoich transakcji.
    /// </remarks>
    public async Task SoftDeleteAsync(Budget budget, DateTimeOffset deletedAt, CancellationToken ct)
    {
        await db.Transactions
            .Where(t => t.BudgetBusinessId == budget.BusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedAt, deletedAt), ct);

        await db.ImportBatches
            .Where(i => i.BudgetId == budget.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.DeletedAt, deletedAt), ct);

        await db.BudgetItems
            .Where(i => i.BudgetBusinessId == budget.BusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.DeletedAt, deletedAt), ct);
    }

    /// <summary>Zdejmuje stempel wyłącznie z dzieci skasowanych razem z budżetem.</summary>
    /// <remarks>
    /// Porównanie po znaczniku czasu — dzieci skasowane wcześniejszym resetem mają inny znacznik
    /// i zostają skasowane. Inaczej przywrócenie po cichu cofnęłoby także reset.
    /// </remarks>
    public async Task RestoreAsync(Budget budget, DateTimeOffset deletedAt, CancellationToken ct)
    {
        await db.Transactions.IgnoreQueryFilters()
            .Where(t => t.BudgetBusinessId == budget.BusinessId && t.DeletedAt == deletedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedAt, (DateTimeOffset?)null), ct);

        await db.ImportBatches.IgnoreQueryFilters()
            .Where(i => i.BudgetId == budget.Id && i.DeletedAt == deletedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.DeletedAt, (DateTimeOffset?)null), ct);

        await db.BudgetItems.IgnoreQueryFilters()
            .Where(i => i.BudgetBusinessId == budget.BusinessId && i.DeletedAt == deletedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.DeletedAt, (DateTimeOffset?)null), ct);
    }
}
