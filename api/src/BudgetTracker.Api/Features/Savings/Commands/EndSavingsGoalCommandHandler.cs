using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>
/// Rezygnacja z celu — kończy go datą, nie kasuje. Dowody z przeszłości mają dalej
/// odnosić się do kwoty, która wtedy obowiązywała.
/// </summary>
/// <remarks>
/// Kończy się BIEŻĄCYM miesiącem, nie poprzednim: cel obowiązywał przez większość tego
/// miesiąca, więc udawanie, że go nie było, byłoby przepisaniem historii w drugą stronę.
/// Rezygnacja z celu, którego nie ma, to 404 — użytkownik ma się dowiedzieć, że patrzy na nieaktualny ekran.
/// </remarks>
public sealed class EndSavingsGoalCommandHandler(AppDbContext db, SavingsBudgetScope scope)
{
    public async Task HandleAsync(Guid? budgetId, CancellationToken ct)
    {
        var today = scope.Today();
        var resolved = await scope.SingleAsync(budgetId, today, ct);

        var active = await db.SavingsGoals
            .Where(g => g.BudgetBusinessId == resolved && g.EndedOn == null)
            .OrderByDescending(g => g.StartedOn).ThenByDescending(g => g.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new SavingsGoalNotFoundException();

        active.End(SavingsMonths.FirstDayOf(today));
        await db.SaveChangesAsync(ct);
    }
}
