using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Queries;

/// <summary>Listy do pól wyboru w ustawieniach kafelków strategii — z budżetu, do którego należy strategia.</summary>
/// <remarks>
/// Kategorie to te same, które dopuszcza ekran limitów (wydatkowe), bo limit i wydatek jednorazowy dotyczą wydatków.
/// Zlecenia stałe: tylko niezakończone — zakończone nie mają czego kończyć.
/// </remarks>
public sealed class GetStrategyReferencesQueryHandler(AppDbContext db, LimitCategories limitCategories)
{
    public async Task<StrategyReferencesResponseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.BusinessId == id, ct)
            ?? throw new StrategyNotFoundException(id);

        var categories = await limitCategories.AllowedAsync(ct);
        var orders = await db.StandingOrders
            .Where(o => o.BudgetBusinessId == strategy.BudgetBusinessId && o.EndMonth == null)
            .OrderBy(o => o.Name)
            .Select(o => new StrategyStandingOrderOptionResponseDto(o.BusinessId, o.Name, o.ExpectedAmount))
            .ToListAsync(ct);

        return new StrategyReferencesResponseDto(
            [.. categories.Select(c => new StrategyCategoryOptionResponseDto(c.BusinessId, c.Name))], orders);
    }
}
