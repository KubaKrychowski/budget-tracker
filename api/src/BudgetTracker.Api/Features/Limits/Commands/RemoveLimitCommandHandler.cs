using BudgetTracker.Api.Features.Limits.Exceptions;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Limits.Commands;

/// <summary>Zdejmuje limit z kategorii od bieżącego miesiąca.</summary>
public sealed class RemoveLimitCommandHandler(AppDbContext db, LimitsBudgetScope scope)
{
    /// <summary>
    /// Kategoria przestaje mieć limit od bieżącego miesiąca; zamknięte miesiące dalej liczą się z limitem, który w nich obowiązywał.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Limit, który objął już zamknięte miesiące, jest KOŃCZONY na poprzednim miesiącu, nie kasowany —
    /// skasowanie wyczyściłoby historię, na którą ktoś już patrzył.</item>
    /// <item>Limit, który jeszcze nie objął żadnego zamkniętego miesiąca, jest kasowany logicznie: nie ma czego chronić.</item>
    /// <item>Zaplanowane późniejsze zmiany tej kategorii też znikają — „usuń limit" znaczy „ta kategoria nie ma
    /// limitu", a nie „nie ma go do czasu następnej zaplanowanej kwoty".</item>
    /// <item>Limit już zakończony przed bieżącym miesiącem to 409: jest historią i nie ma czego zdejmować.</item>
    /// </list>
    /// </remarks>
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var item = await db.BudgetItems.FirstOrDefaultAsync(i => i.BusinessId == id, ct)
            ?? throw new LimitNotFoundException(id);

        var currentMonth = scope.CurrentMonth();
        if (item.ValidTo is { } to && to < currentMonth) throw new LimitHistoryLockedException();

        var later = await db.BudgetItems
            .Where(i => i.BudgetBusinessId == item.BudgetBusinessId && i.CategoryId == item.CategoryId
                        && i.ValidFrom > item.ValidFrom)
            .ToListAsync(ct);
        db.BudgetItems.RemoveRange(later);

        if (item.ValidFrom >= currentMonth) db.BudgetItems.Remove(item);
        else item.End(currentMonth.AddMonths(-1));

        await db.SaveChangesAsync(ct);
    }
}
