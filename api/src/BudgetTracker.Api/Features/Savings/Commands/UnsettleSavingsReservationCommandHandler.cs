using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>
/// Cofnięcie rozliczenia rezerwacji.
/// </summary>
/// <remarks>
/// Istnieje dlatego, że wskazanie złej transakcji jest łatwe, a bez tej operacji byłoby drzwiami w jedną stronę:
/// jedynym wyjściem byłoby skasowanie rezerwacji i założenie jej od nowa, czyli utrata terminu i miejsca w kolejce.
/// </remarks>
public sealed class UnsettleSavingsReservationCommandHandler(
    AppDbContext db, ReservationLookup reservations, SavingsBudgetScope scope)
{
    public async Task<SavingsReservationResponseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var reservation = await reservations.FindAsync(id, ct);

        reservation.Unsettle();
        await db.SaveChangesAsync(ct);

        return ReservationViews.Single(reservation, SavingsMonths.FirstDayOf(scope.Today()));
    }
}
