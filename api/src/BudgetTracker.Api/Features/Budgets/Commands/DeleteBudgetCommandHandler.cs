using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Usunięcie: to samo co reset PLUS stempel na samym budżecie. Fizycznie dane znikają dopiero
/// po oknie retencji (<see cref="BudgetOptions.RetentionDays"/>).
/// </summary>
public sealed class DeleteBudgetCommandHandler(
    AppDbContext db, BudgetLookup lookup, BudgetChildren children, SavingsTransferMatcher savingsTransfers, TimeProvider clock)
{
    /// <remarks>
    /// <list type="bullet">
    /// <item>⚠️ Budżet i dzieci dostają TEN SAM znacznik z jednego odczytu zegara — przywracanie rozpoznaje
    /// dzieci po równości znaczników (patrz <see cref="BudgetChildren.SoftDeleteAsync"/>).</item>
    /// <item>Budżet, który wskazywał USUWANY jako powiązany oszczędnościowy, traci to powiązanie i jego
    /// reguły — inaczej zostałoby martwe wskazanie na budżet, którego nie widać. Przywrócenie usuniętego
    /// budżetu oszczędnościowego NIE oddaje powiązania: użytkownik wskazuje je ponownie świadomie.</item>
    /// </list>
    /// </remarks>
    public async Task HandleAsync(Guid businessId, CancellationToken ct)
    {
        var budget = await lookup.FindIncludingDeletedAsync(businessId, ct);
        var now = clock.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var linkers = await db.Budgets
            .Where(b => b.LinkedSavingsBudgetBusinessId == businessId)
            .ToListAsync(ct);
        foreach (var linker in linkers)
        {
            await savingsTransfers.ForgetAsync(linker.BusinessId, businessId, ct);
            linker.LinkSavingsBudget(null);
            linker.ReplaceSavingsTransferRules([]);
        }

        await children.SoftDeleteAsync(budget, now, ct);

        // ⚠️ Cele i rezerwacje osobnym wywołaniem, bo RESET ich nie zabiera — patrz
        // <see cref="BudgetChildren.SoftDeleteSavingsAsync"/>. Ten sam znacznik czasu, inaczej
        // przywracanie (porównujące znaczniki) wróciłoby bez nich.
        await children.SoftDeleteSavingsAsync(budget, now, ct);

        budget.MarkDeleted(now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
