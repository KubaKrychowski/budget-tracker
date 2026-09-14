using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>„Wpłać na cel” i „Wycofaj” — umowne odkładanie oszczędności na rezerwację (zgłoszenie #23).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Pieniądze nie zmieniają konta — wpłata mówi tylko, na co odłożona część jest przeznaczona.</item>
/// <item>Nie wpłacisz więcej, niż BRAKUJE do kwoty rezerwacji (400) ani więcej, niż zostało na koncie po wpłatach
/// na inne cele w tym budżecie (400) — inaczej suma „uzbieranego” przekraczałaby oszczędności.</item>
/// <item>Rozliczona rezerwacja jest zamknięta: wpłata na nią to 409, jak drugie rozliczenie.</item>
/// </list>
/// </remarks>
public sealed class ContributeToReservationCommandHandler(
    AppDbContext db, ReservationLookup reservations, SavingsAccount account, SavingsBudgetScope scope)
{
    public async Task<SavingsReservationResponseDto> ContributeAsync(Guid id, ContributeRequestDto request, CancellationToken ct)
    {
        var reservation = await reservations.FindAsync(id, ct);
        if (reservation.SettledAt is not null) throw new ReservationAlreadySettledException();
        if (request.Amount <= 0 || request.Amount > reservation.Amount - reservation.Contributed)
        {
            throw new ContributionAmountInvalidException();
        }

        IReadOnlyList<Guid> budget = [reservation.BudgetBusinessId];
        var available = await account.BalanceAsync(budget, ct) - await account.ContributedAsync(budget, ct);
        if (request.Amount > available) throw new ContributionExceedsBalanceException();

        var today = scope.Today();
        reservation.Contribute(today, request.Amount);
        await db.SaveChangesAsync(ct);

        return ReservationViews.Single(reservation, SavingsMonths.FirstDayOf(today));
    }

    public async Task<SavingsReservationResponseDto> WithdrawAsync(Guid id, Guid contributionId, CancellationToken ct)
    {
        var reservation = await reservations.FindAsync(id, ct);
        if (reservation.SettledAt is not null) throw new ReservationAlreadySettledException();
        if (!reservation.WithdrawContribution(contributionId)) throw new ContributionNotFoundException(contributionId);

        await db.SaveChangesAsync(ct);
        return ReservationViews.Single(reservation, SavingsMonths.FirstDayOf(scope.Today()));
    }
}
