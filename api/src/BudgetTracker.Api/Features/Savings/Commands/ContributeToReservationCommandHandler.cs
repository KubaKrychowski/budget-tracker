using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>„Wpłać na cel” i „Wycofaj” — umowne odkładanie pieniędzy na rezerwację (zgłoszenie #23).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Pieniądze nie zmieniają konta — wpłata mówi tylko, na co odłożona część jest przeznaczona.</item>
/// <item>Nie wpłacisz więcej, niż BRAKUJE do kwoty rezerwacji (400), z obu kont łącznie.</item>
/// <item>Z konta oszczędnościowego nie wpłacisz więcej, niż zostało na nim po wpłatach na inne cele w tym budżecie
/// (400) — inaczej suma „uzbieranego” przekraczałaby oszczędności.</item>
/// <item>⚠️ Ze zwykłego konta wpłata wymaga kategorii, na którą da się ustawić limit (400), i wlicza się do jej
/// limitu w okresie daty wpłaty (<see cref="LimitSpending"/>). Stanu oszczędności nie rusza i nie ma własnego
/// pułapu — stanu zwykłego konta nie da się odjąć od czegokolwiek sensownego, bo wydatki wchodzą z wyciągów.</item>
/// <item>Rozliczona rezerwacja jest zamknięta: wpłata na nią to 409, jak drugie rozliczenie.</item>
/// </list>
/// </remarks>
public sealed class ContributeToReservationCommandHandler(
    AppDbContext db, ReservationLookup reservations, SavingsAccount account, SavingsBudgetScope scope,
    LimitCategories limitCategories)
{
    public async Task<SavingsReservationResponseDto> ContributeAsync(Guid id, ContributeRequestDto request, CancellationToken ct)
    {
        var reservation = await reservations.FindAsync(id, ct);
        if (reservation.SettledAt is not null) throw new ReservationAlreadySettledException();
        if (request.Amount <= 0 || request.Amount > reservation.Amount - reservation.Contributed)
        {
            throw new ContributionAmountInvalidException();
        }

        Guid? categoryId = request.Source == ContributionSource.Regular
            ? await ValidCategoryAsync(request.CategoryId, ct)
            : null;

        if (request.Source == ContributionSource.Savings)
        {
            IReadOnlyList<Guid> budget = [reservation.BudgetBusinessId];
            var available = await account.BalanceAsync(budget, ct) - await account.ContributedAsync(budget, ct);
            if (request.Amount > available) throw new ContributionExceedsBalanceException();
        }

        var today = scope.Today();
        reservation.Contribute(today, request.Amount, request.Source, categoryId);
        await db.SaveChangesAsync(ct);

        return ReservationViews.Single(reservation, SavingsMonths.FirstDayOf(today));
    }

    /// <summary>Kategoria wpłaty ze zwykłego konta musi być jedną z tych, na które da się ustawić limit.</summary>
    private async Task<Guid> ValidCategoryAsync(Guid? categoryId, CancellationToken ct)
    {
        var allowed = await limitCategories.AllowedAsync(ct);
        return categoryId is { } id && allowed.Any(c => c.BusinessId == id)
            ? id
            : throw new ContributionCategoryInvalidException();
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
