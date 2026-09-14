using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Exceptions;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.EpisodicOrders.Commands;

/// <summary>„Załóż cel oszczędzania” — rezerwacja z nazwą, kwotą i terminem zaplanowanego zlecenia.</summary>
/// <remarks>
/// <para>
/// Bez nowego bytu (decyzja użytkownika): to zwykła rezerwacja — uzbierane rośnie dopiero z wpłatami na ekranie
/// rezerwacji. Priorytet 0, jak domyślny w modalu rezerwacji. Zlecenie bez terminu zakłada rezerwację „przy okazji”.
/// </para>
/// <para>
/// 409 dla zrealizowanego (nie ma już na co zbierać) i dla zlecenia, które rezerwację już ma — drugi klik
/// zakładałby drugą kopertę na ten sam zakup.
/// </para>
/// </remarks>
public sealed class CreateEpisodicOrderReservationCommandHandler(
    AppDbContext db, EpisodicOrderLookup orders, EpisodicOrdersBudgetScope scope)
{
    public async Task<EpisodicOrderSavedResponseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var order = await orders.FindAsync(id, ct);
        if (order.IsRealized || order is not { PlannedAmount: { } amount }
            || await orders.ReservationOfAsync(order, ct) is not null)
        {
            throw new EpisodicOrderStateConflictException();
        }

        var reservation = new SavingsReservation(order.BudgetBusinessId, order.Name, amount, order.DueMonth, 0, scope.Now());
        order.AttachReservation(reservation.BusinessId);
        db.SavingsReservations.Add(reservation);
        await db.SaveChangesAsync(ct);

        return new EpisodicOrderSavedResponseDto(reservation.BusinessId);
    }
}
