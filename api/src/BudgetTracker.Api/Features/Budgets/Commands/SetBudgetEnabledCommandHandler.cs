using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Wyłączenie i włączenie budżetu — jedno pole, więc jeden handler z flagą.
/// </summary>
/// <remarks>
/// ⚠️ Wyłączenie zamyka budżet na NOWE DANE, nie na zarządzanie — edycja, reset, usunięcie
/// i przywrócenie działają na nim tak samo jak na aktywnym.
/// </remarks>
public sealed class SetBudgetEnabledCommandHandler(
    AppDbContext db, BudgetLookup lookup, BudgetListItemReader reader, TimeProvider clock)
{
    public async Task<BudgetListItemResponseDto> HandleAsync(Guid businessId, bool enabled, CancellationToken ct)
    {
        var budget = await lookup.FindIncludingDeletedAsync(businessId, ct);
        if (enabled) budget.Enable();
        else budget.Disable(clock.GetUtcNow());

        await db.SaveChangesAsync(ct);
        return await reader.ReadOneAsync(businessId, ct);
    }
}
