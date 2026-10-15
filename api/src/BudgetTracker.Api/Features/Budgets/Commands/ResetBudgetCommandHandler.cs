using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Reset: transakcje, wpisy importów i pozycje limitów dostają <c>DeletedAt</c>,
/// budżet zostaje żywy z nazwą, walutą, miesiącem i bilansem początkowym.
/// </summary>
/// <remarks>
/// Bilans początkowy to punkt, OD którego budżet liczy — reset czyści to, co się w nim
/// wydarzyło, a nie sam punkt odniesienia. Paragony transakcji budżetu są usuwane razem z plikami (<see cref="ReceiptCleaner"/>).
/// </remarks>
public sealed class ResetBudgetCommandHandler(
    AppDbContext db, BudgetLookup lookup, BudgetChildren children, ReceiptCleaner receipts, BudgetListItemReader reader, TimeProvider clock)
{
    public async Task<BudgetListItemResponseDto> HandleAsync(Guid businessId, CancellationToken ct)
    {
        var budget = await lookup.FindIncludingDeletedAsync(businessId, ct);

        await receipts.DeleteForBudgetsAsync([budget.BusinessId], ct);

        await children.SoftDeleteAsync(budget, clock.GetUtcNow(), ct);

        return await reader.ReadOneAsync(businessId, ct);
    }
}
