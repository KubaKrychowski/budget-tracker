using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.EpisodicOrders.Commands;

/// <summary>Usuwa zlecenie epizodyczne. Transakcja zostaje nietknięta.</summary>
/// <remarks>
/// Nierozliczona rezerwacja znika razem ze zleceniem — zostawiona zbierałaby dalej na zakup, którego już nikt nie
/// planuje, i zaniżała wolne środki. Rozliczona zostaje: to historia wypłaty z oszczędności, nie plan.
/// </remarks>
public sealed class DeleteEpisodicOrderCommandHandler(AppDbContext db, EpisodicOrderLookup orders)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var order = await orders.FindAsync(id, ct);
        var reservation = await orders.ReservationOfAsync(order, ct);

        if (reservation is { SettledAt: null }) db.SavingsReservations.Remove(reservation);
        db.EpisodicOrders.Remove(order);
        await db.SaveChangesAsync(ct);
    }
}
