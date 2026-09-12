using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Usunięcie: to samo co reset PLUS stempel na samym budżecie. Fizycznie dane znikają dopiero
/// po oknie retencji (<see cref="BudgetOptions.RetentionDays"/>).
/// </summary>
public sealed class DeleteBudgetCommandHandler(
    AppDbContext db, BudgetLookup lookup, BudgetChildren children, TimeProvider clock)
{
    /// <remarks>
    /// ⚠️ Budżet i dzieci dostają TEN SAM znacznik z jednego odczytu zegara — przywracanie rozpoznaje
    /// dzieci po równości znaczników (patrz <see cref="BudgetChildren.SoftDeleteAsync"/>).
    /// </remarks>
    public async Task HandleAsync(Guid businessId, CancellationToken ct)
    {
        var budget = await lookup.FindIncludingDeletedAsync(businessId, ct);
        var now = clock.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await children.SoftDeleteAsync(budget, now, ct);
        budget.MarkDeleted(now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
