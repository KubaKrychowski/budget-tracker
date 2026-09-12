using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>Rezerwacja po publicznym identyfikatorze — wspólne dla edycji, rozliczenia i kandydatów.</summary>
public sealed class ReservationLookup(AppDbContext db)
{
    /// <summary>Rezerwacja o <paramref name="id"/>; nieznana kończy się 404.</summary>
    public async Task<SavingsReservation> FindAsync(Guid id, CancellationToken ct) =>
        await db.SavingsReservations.FirstOrDefaultAsync(r => r.BusinessId == id, ct)
        ?? throw new SavingsReservationNotFoundException(id);
}
