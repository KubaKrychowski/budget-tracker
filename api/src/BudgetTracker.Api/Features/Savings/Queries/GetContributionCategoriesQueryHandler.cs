using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Queries;

/// <summary>Kategorie do wyboru przy wpłacie ze zwykłego konta razem z tym, ile limitu już poszło.</summary>
/// <remarks>
/// Osobny odczyt per rezerwacja, nie pole listy: lista może pokazywać kilka budżetów naraz, a limit i okres
/// należą do JEDNEGO — budżetu rezerwacji, na którą wpłacasz.
/// </remarks>
public sealed class GetContributionCategoriesQueryHandler(
    AppDbContext db, ReservationLookup reservations, LimitCategories limitCategories, LimitSpending limitSpending,
    LimitsBudgetScope limitsScope)
{
    /// <summary>Dozwolone kategorie limitu, alfabetycznie; wydane liczy się w bieżącym okresie budżetu rezerwacji.</summary>
    public async Task<IReadOnlyList<ContributionCategoryResponseDto>> HandleAsync(Guid reservationId, CancellationToken ct)
    {
        var reservation = await reservations.FindAsync(reservationId, ct);
        var budget = reservation.BudgetBusinessId;

        var period = await limitsScope.PeriodAsync(budget, ct);
        var key = period.CurrentKey;

        var allowed = await limitCategories.AllowedAsync(ct);
        var limits = (await db.BudgetItems
                .Where(i => i.BudgetBusinessId == budget)
                .OrderBy(i => i.ValidFrom).ThenBy(i => i.Id)
                .ToListAsync(ct))
            .Where(i => i.AppliesTo(key))
            .ToDictionary(i => i.CategoryId, i => i.Limit);

        var spent = await limitSpending.ByCategoryAsync(budget, period.From(key), period.ToExclusive(key), ct);

        return
        [
            .. allowed.Select(c => new ContributionCategoryResponseDto(
                c.BusinessId, c.Name,
                limits.TryGetValue(c.Id, out var limit) ? limit : null,
                spent.GetValueOrDefault(c.Id)?.Spent ?? 0m)),
        ];
    }
}
