using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Przywrócenie w oknie retencji — cofa usunięcie budżetu i jego dzieci.
/// </summary>
/// <remarks>
/// ⚠️ Tego NIE MA w makiecie: menu wiersza ma tam cztery pozycje bez przywracania. Bez tej
/// operacji obietnica z modala („odwracalna przez 30 dni") nie miałaby jak się spełnić.
/// Wracają tylko dzieci skasowane RAZEM z budżetem — patrz <see cref="BudgetChildren.RestoreAsync"/>.
/// </remarks>
public sealed class RestoreBudgetCommandHandler(
    AppDbContext db, BudgetLookup lookup, BudgetChildren children, BudgetListItemReader reader)
{
    public async Task<BudgetListItemResponseDto> HandleAsync(Guid businessId, CancellationToken ct)
    {
        var budget = await lookup.FindIncludingDeletedAsync(businessId, ct);
        if (budget.DeletedAt is not { } deletedAt) return await reader.ReadOneAsync(businessId, ct);

        await children.RestoreAsync(budget, deletedAt, ct);
        budget.Restore();
        await db.SaveChangesAsync(ct);

        return await reader.ReadOneAsync(businessId, ct);
    }
}
