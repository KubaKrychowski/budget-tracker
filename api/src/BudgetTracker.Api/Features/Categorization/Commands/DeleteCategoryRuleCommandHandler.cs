using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>Usuwa regułę kategoryzacji logicznie.</summary>
/// <remarks>
/// Kasowanie logiczne — <c>SoftDeleteInterceptor</c> zamienia <c>Remove</c> na ustawienie <c>DeletedAt</c>.
/// Reguła bywa jedynym wyjaśnieniem, czemu setki transakcji mają daną kategorię; twarde usunięcie zabierałoby
/// tę odpowiedź razem z wierszem.
/// </remarks>
public sealed class DeleteCategoryRuleCommandHandler(AppDbContext db, CategoryRuleLookup lookup)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        db.Set<CategoryRule>().Remove(await lookup.FindOwnAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }
}
