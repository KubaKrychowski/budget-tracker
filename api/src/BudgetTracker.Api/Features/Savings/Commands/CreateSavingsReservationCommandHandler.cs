using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>Zakłada rezerwację na wskazanym (albo domyślnym) budżecie.</summary>
public sealed class CreateSavingsReservationCommandHandler(
    AppDbContext db, SavingsBudgetScope scope, TimeProvider clock, ICurrentUserAccessor currentUser)
{
    public async Task<SavingsReservationResponseDto> HandleAsync(SaveReservationRequestDto request, CancellationToken ct)
    {
        var (name, amount) = ReservationRequestValidator.Validate(request);
        var today = scope.Today();
        var budgetId = await scope.SingleAsync(request.BudgetId, today, ct);

        var reservation = new SavingsReservation(
            budgetId,
            name,
            amount,
            SavingsMonths.FirstDayOf(request.DueMonth),
            request.Priority,
            clock.GetUtcNow(),
            currentUser.UserId ?? default);

        db.SavingsReservations.Add(reservation);
        await db.SaveChangesAsync(ct);

        return ReservationViews.Single(reservation, SavingsMonths.FirstDayOf(today));
    }
}
