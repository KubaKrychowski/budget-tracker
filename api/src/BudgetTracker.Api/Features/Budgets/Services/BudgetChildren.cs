using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>
/// „Kaskada" w kodzie, nie w bazie (CLAUDE.md §4): transakcje, wpisy importów, limity, cele
/// oszczędzania i rezerwacje są stemplowane i przywracane razem z budżetem.
/// </summary>
/// <remarks>
/// <para>
/// Transakcje, limity, cele i rezerwacje łączą się z budżetem po <c>BusinessId</c> (bez relacji EF),
/// importy dalej po <c>Id</c>. <c>ExecuteUpdate</c> zamiast wczytywania encji: transakcji bywa ponad
/// tysiąc na budżet, więc materializowanie ich tylko po to, by ustawić jedno pole, byłoby marnotrawstwem.
/// </para>
/// <para>
/// ⚠️ <b>Cele i rezerwacje doszły tu 2026-09-12</b> i to nie było rozszerzenie zakresu, tylko naprawa.
/// Powstały PO tej klasie, więc nikt ich do niej nie dopisał: usunięcie budżetu zostawiało żywy cel
/// wskazujący na budżet, którego już nie widać, a przywrócenie budżetu wracało bez niego. Na bazie
/// deweloperskiej autora było z tego 1 cel i 12 rezerwacji bez żadnego budżetu.
/// </para>
/// <para>
/// ⚠️ RESET budżetu ich NIE dotyczy i to jest świadome: cel i rezerwacja są ZASADĄ, nie danymi —
/// przeżywają reset tak samo jak nazwa, waluta i bilans początkowy. Reset czyści to, co wpadło
/// do budżetu, a nie to, co o nim postanowiono (patrz <c>ResetBudgetCommandHandler</c>).
/// </para>
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

    /// <summary>
    /// Stempluje CELE OSZCZĘDZANIA, REZERWACJE i ZLECENIA (stałe i epizodyczne) budżetu — osobno od <see cref="SoftDeleteAsync"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Osobna metoda, bo <see cref="SoftDeleteAsync"/> ma DWÓCH wywołujących o różnych intencjach:
    /// usunięcie budżetu i jego RESET. Reset czyści to, co wpadło do budżetu (transakcje, importy,
    /// limity), ale cel i rezerwacja są ZASADĄ, nie danymi — przeżywają reset tak samo jak nazwa,
    /// waluta i bilans początkowy. Dopisanie oszczędności do tamtej metody kasowało cel przy resecie,
    /// czego nikt nie zamawiał; złapał to test <c>Reset_leaves_the_goal_and_reservations_alone</c>.
    ///
    /// Woła to wyłącznie usunięcie budżetu. Znacznik musi być TEN SAM co budżetu, bo przywracanie
    /// rozpoznaje dzieci po równości znaczników.
    /// </remarks>
    public async Task SoftDeleteSavingsAsync(Budget budget, DateTimeOffset deletedAt, CancellationToken ct)
    {
        await db.SavingsGoals
            .Where(g => g.BudgetBusinessId == budget.BusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.DeletedAt, deletedAt), ct);

        await db.SavingsReservations
            .Where(r => r.BudgetBusinessId == budget.BusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeletedAt, deletedAt), ct);

        // Zlecenia stałe i epizodyczne są tą samą grupą: ZASADA budżetu, nie dane — reset je zostawia, usunięcie zabiera.
        await db.StandingOrders
            .Where(o => o.BudgetBusinessId == budget.BusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.DeletedAt, deletedAt), ct);

        await db.EpisodicOrders
            .Where(o => o.BudgetBusinessId == budget.BusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.DeletedAt, deletedAt), ct);
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

        await db.SavingsGoals.IgnoreQueryFilters()
            .Where(g => g.BudgetBusinessId == budget.BusinessId && g.DeletedAt == deletedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.DeletedAt, (DateTimeOffset?)null), ct);

        await db.SavingsReservations.IgnoreQueryFilters()
            .Where(r => r.BudgetBusinessId == budget.BusinessId && r.DeletedAt == deletedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeletedAt, (DateTimeOffset?)null), ct);

        await db.StandingOrders.IgnoreQueryFilters()
            .Where(o => o.BudgetBusinessId == budget.BusinessId && o.DeletedAt == deletedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.DeletedAt, (DateTimeOffset?)null), ct);

        await db.EpisodicOrders.IgnoreQueryFilters()
            .Where(o => o.BudgetBusinessId == budget.BusinessId && o.DeletedAt == deletedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.DeletedAt, (DateTimeOffset?)null), ct);

    }
}
