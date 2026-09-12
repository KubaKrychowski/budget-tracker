using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>
/// Edycja nazwy, kwoty, terminu i priorytetu rezerwacji.
/// </summary>
/// <remarks>
/// ⚠️ Budżetu NIE da się zmienić — przeniesienie rezerwacji między budżetami przesunęłoby
/// ją do innej puli i innej kolejki naraz, a wskazana wypłata została w starym budżecie.
/// Kto chce przenieść, zakłada nową.
/// </remarks>
public sealed class UpdateSavingsReservationCommandHandler(
    AppDbContext db, ReservationLookup reservations, SavingsBudgetScope scope)
{
    public async Task<SavingsReservationResponseDto> HandleAsync(
        Guid id, SaveReservationRequestDto request, CancellationToken ct)
    {
        var (name, amount) = ReservationRequestValidator.Validate(request);
        var reservation = await reservations.FindAsync(id, ct);

        reservation.Update(name, amount, SavingsMonths.FirstDayOf(request.DueMonth), request.Priority);

        await db.SaveChangesAsync(ct);

        return ReservationViews.Single(reservation, SavingsMonths.FirstDayOf(scope.Today()));
    }
}
