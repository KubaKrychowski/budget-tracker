using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Queries;

/// <summary>
/// Kandydaci do rozliczenia rezerwacji — nierozliczone wypłaty z oszczędności w tym samym budżecie.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Miesiąc terminu nie jest filtrem, tylko drugim kryterium kolejności.</b> Makieta
/// proponuje wypłatę z sierpnia dla rezerwacji z terminem majowym — rachunki płaci się po
/// terminie, więc twardy filtr miesiąca nigdy by tego kandydata nie pokazał. Pierwsze
/// kryterium to zgodność kwoty, bo to ona rozpoznaje wydatek.
/// </para>
/// <para>
/// Sortowanie w pamięci: różnica kwot i odległość miesięcy nie tłumaczą się na SQL
/// sensownie, a zbiór (wypłaty z oszczędności w jednym budżecie) jest z natury mały.
/// </para>
/// </remarks>
public sealed class GetSettleCandidatesQueryHandler(
    AppDbContext db, ReservationLookup reservations, SavingsCategory savingsCategory)
{
    /// <summary>Ilu kandydatów do rozliczenia pokazać. Modal proponuje jednego, reszta to zapas.</summary>
    private const int MaxSettleCandidates = 10;

    public async Task<IReadOnlyList<SettleCandidateResponseDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        var reservation = await reservations.FindAsync(id, ct);
        var savingsCategoryId = await savingsCategory.IdAsync(ct);
        if (savingsCategoryId is null) return [];

        var used = await db.SavingsReservations
            .Where(r => r.SettledTransactionBusinessId != null && r.Id != reservation.Id)
            .Select(r => r.SettledTransactionBusinessId!.Value)
            .ToListAsync(ct);

        var rows = await db.Transactions
            .Where(t => t.BudgetBusinessId == reservation.BudgetBusinessId
                        && t.CategoryId == savingsCategoryId
                        && t.Amount > 0
                        && !used.Contains(t.BusinessId))
            .Select(t => new SettleCandidateResponseDto(t.BusinessId, t.Date, t.Amount, t.Description))
            .ToListAsync(ct);

        return [.. rows
            .OrderBy(c => Math.Abs(c.Amount - reservation.Amount))
            .ThenBy(c => Math.Abs(SavingsMonths.MonthsBetween(SavingsMonths.FirstDayOf(c.Date), reservation.DueMonth)))
            .ThenByDescending(c => c.Date)
            .Take(MaxSettleCandidates)];
    }
}
