using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.EpisodicOrders.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.EpisodicOrders.Services;

/// <summary>Zlecenie epizodyczne po publicznym identyfikatorze — nieznane to 404, nigdy cichy fallback.</summary>
public sealed class EpisodicOrderLookup(AppDbContext db)
{
    public async Task<EpisodicOrder> FindAsync(Guid id, CancellationToken ct) =>
        await db.EpisodicOrders.FirstOrDefaultAsync(o => o.BusinessId == id, ct)
        ?? throw new EpisodicOrderNotFoundException(id);

    /// <summary>Rezerwacja zlecenia; <c>null</c>, gdy zlecenie jej nie ma albo ją skasowano na ekranie rezerwacji.</summary>
    public async Task<SavingsReservation?> ReservationOfAsync(EpisodicOrder order, CancellationToken ct) =>
        order.ReservationBusinessId is { } id
            ? await db.SavingsReservations.FirstOrDefaultAsync(r => r.BusinessId == id, ct)
            : null;
}
