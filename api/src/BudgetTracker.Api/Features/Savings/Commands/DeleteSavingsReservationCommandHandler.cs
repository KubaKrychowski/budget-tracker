using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>Usuwa rezerwację logicznie — <c>db.Remove</c> znaczy tu „ostempluj" (<c>SoftDeleteInterceptor</c>).</summary>
public sealed class DeleteSavingsReservationCommandHandler(AppDbContext db, ReservationLookup reservations)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        db.SavingsReservations.Remove(await reservations.FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }
}
