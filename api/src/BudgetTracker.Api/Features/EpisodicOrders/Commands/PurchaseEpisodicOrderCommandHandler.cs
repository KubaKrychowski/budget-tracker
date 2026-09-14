using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Exceptions;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.EpisodicOrders.Commands;

/// <summary>„Oznacz jako kupione…” i „Cofnij do zaplanowanych”.</summary>
/// <remarks>
/// <para>
/// ⚠️ Kupienie ROZLICZA nierozliczoną rezerwację zlecenia tą samą transakcją, bez warunków rozliczenia z ekranu
/// rezerwacji (kategoria „Oszczędności”, kwota dodatnia). Tamte warunki rozpoznają WYPŁATĘ z konta oszczędnościowego;
/// tu rezerwację zamyka sam ZAKUP, na który zbierała — dialog mówi o tym wprost. Wolnych środków to nie zmienia,
/// tak samo jak każde rozliczenie.
/// </para>
/// <para>
/// Cofnięcie jest tylko dla zlecenia, które było zaplanowane — oznaczone wprost z listy transakcji nie ma planu,
/// do którego mogłoby wrócić (409). Rezerwacja wraca do kolejki zbierania z tym samym terminem.
/// </para>
/// </remarks>
public sealed class PurchaseEpisodicOrderCommandHandler(
    AppDbContext db, EpisodicOrderLookup orders, EpisodicOrderTransactions transactions, EpisodicOrdersBudgetScope scope)
{
    public async Task PurchaseAsync(Guid id, PurchaseEpisodicOrderRequestDto request, CancellationToken ct)
    {
        var order = await orders.FindAsync(id, ct);
        if (order.IsRealized) throw new EpisodicOrderStateConflictException();

        var transaction = await transactions.RequireUsableAsync(
            order.BudgetBusinessId, request.TransactionId, order.BusinessId, ct);
        order.Realize(transaction.BusinessId);

        var reservation = await orders.ReservationOfAsync(order, ct);
        if (reservation is { SettledAt: null }) reservation.Settle(scope.Now(), transaction.BusinessId);

        await db.SaveChangesAsync(ct);
    }

    public async Task UndoAsync(Guid id, CancellationToken ct)
    {
        var order = await orders.FindAsync(id, ct);
        if (!order.IsRealized || !order.WasPlanned) throw new EpisodicOrderStateConflictException();

        var reservation = await orders.ReservationOfAsync(order, ct);
        if (reservation is not null && reservation.SettledTransactionBusinessId == order.TransactionBusinessId)
        {
            reservation.Unsettle();
        }

        order.Unrealize();
        await db.SaveChangesAsync(ct);
    }
}
