using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Queries;

/// <summary>
/// Rezerwacje na koncie oszczędnościowym — nazwane koperty na nadchodzące wydatki — razem z wolnymi środkami.
/// </summary>
/// <remarks>
/// <para>
/// Domyka lukę, którą zostawił cel oszczędnościowy (#7): <b>„wolne środki" nie miały
/// definicji</b>. Bez rezerwacji kafel pokazywałby całe oszczędności jako dostępne — w tym
/// pieniądze przypisane już na ubezpieczenie.
/// </para>
/// <para>
/// ⚠️ <b>Uzbierane to suma WPŁAT</b> (zgłoszenie #23), a nie kolejka rozkładająca stan konta od najbliższego terminu.
/// Kolejka pokazywała 100% zaraz po założeniu rezerwacji przy pełnym koncie, choć użytkownik nic na nią nie odłożył.
/// </para>
/// <para>
/// ⚠️ Osobny odczyt, nie rozrost <see cref="GetSavingsQueryHandler"/>: tamten liczy DYSCYPLINĘ
/// (ile odkładasz co miesiąc), ten liczy STAN (co z odłożonego jest wolne).
/// </para>
/// </remarks>
public sealed class GetSavingsReservationsQueryHandler(AppDbContext db, SavingsBudgetScope scope, SavingsAccount account)
{
    /// <summary>Rezerwacje wybranych budżetów: najbliższy termin wyżej, „przy okazji” na końcu.</summary>
    public async Task<SavingsReservationsResponseDto> HandleAsync(
        IReadOnlyList<Guid>? budgetIds, CancellationToken ct)
    {
        var today = scope.Today();
        var currentMonth = SavingsMonths.FirstDayOf(today);

        var budgets = await scope.OptionsAsync(ct);
        var selected = SavingsBudgetScope.Resolve(budgets, budgetIds, today);
        if (selected.Count == 0)
        {
            return new SavingsReservationsResponseDto([], 0m, 0m, 0m, 0m, 0m, 0m, null, [], budgets);
        }

        var reservations = await db.SavingsReservations
            .Where(r => selected.Contains(r.BudgetBusinessId))
            .ToListAsync(ct);

        var balance = await account.BalanceAsync(selected, ct);
        var open = reservations.Where(r => r.SettledAt is null).ToList();
        var reservedTotal = open.Sum(r => r.Amount);
        var collectedTotal = open.Sum(r => r.Contributed);

        return new SavingsReservationsResponseDto(
            Reservations: [.. reservations
                .OrderBy(r => r.DueMonth is null).ThenBy(r => r.DueMonth).ThenBy(r => r.Priority).ThenBy(r => r.Id)
                .Select(r => ReservationViews.Single(r, currentMonth))],
            AccountBalance: balance,
            ReservedTotal: reservedTotal,
            SettledTotal: reservations.Where(r => r.SettledAt is not null).Sum(r => r.Amount),
            CollectedTotal: collectedTotal,
            AvailableToContribute: balance - collectedTotal,
            FreeFunds: balance - reservedTotal,
            CoveredBy: await CoveredByAsync(selected, reservedTotal - collectedTotal, currentMonth, ct),
            SelectedBudgetIds: selected,
            Budgets: budgets);
    }

    /// <summary>
    /// „Przy celu 1 500 zł miesięcznie resztę rezerwacji uzbierasz do maja" — zdanie z makiety,
    /// i jedyne miejsce, w którym karta celu i karta rezerwacji przestają być sąsiadami
    /// z przypadku.
    /// </summary>
    /// <remarks>
    /// <c>null</c> bez celu: bez tempa nie ma z czego liczyć terminu, a zgadnięcie go byłoby
    /// obietnicą bez pokrycia.
    /// </remarks>
    private async Task<DateOnly?> CoveredByAsync(
        IReadOnlyList<Guid> budgetIds, decimal missing, DateOnly currentMonth, CancellationToken ct)
    {
        if (missing <= 0) return null;

        var monthlyGoal = await db.SavingsGoals
            .Where(g => budgetIds.Contains(g.BudgetBusinessId) && g.EndedOn == null)
            .SumAsync(g => (decimal?)g.Amount, ct) ?? 0m;

        if (monthlyGoal <= 0) return null;

        var months = (int)Math.Ceiling(missing / monthlyGoal);
        return currentMonth.AddMonths(months);
    }
}
