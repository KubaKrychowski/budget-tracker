using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Limits.Contracts;
using BudgetTracker.Api.Features.Limits.Exceptions;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Limits.Commands;

/// <summary>Ustawia albo zmienia limit kategorii w budżecie.</summary>
public sealed class SetLimitCommandHandler(
    AppDbContext db, LimitsBudgetScope scope, LimitCategories limitCategories, ICurrentUserAccessor currentUser)
{
    /// <summary>
    /// Ustawia limit od wskazanego miesiąca. Zmiana KOŃCZY poprzedni limit miesiąc wcześniej i zakłada nowy — nie nadpisuje.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>⚠️ <b>Historia jest zamknięta.</b> Jeśli kategoria miała już kiedyś limit w tym budżecie, nowy
    /// wolno ustawić najwcześniej od bieżącego miesiąca — inaczej zmiana przepisałaby miesiące, które już się
    /// zamknęły (409). PIERWSZY limit kategorii wolno zadeklarować wstecz: bez tego cała dotychczasowa historia
    /// zostaje „bez limitu" i ekran nie ma czego pokazać po cofnięciu się w czasie.</item>
    /// <item>Ten sam miesiąc startu = poprawka w miejscu (<see cref="BudgetItem.Correct"/>), a nie drugi wiersz.</item>
    /// <item>Ta sama kwota i próg co limit, który już obowiązuje w tym miesiącu, to nie zmiana — pocięłaby historię
    /// na kawałki, z których żaden niczego nie zmienia.</item>
    /// <item>Limity zaplanowane PÓŹNIEJ niż nowy start (jeszcze nie obowiązywały) są kasowane — nowa decyzja je zastępuje.
    /// ⚠️ Wycofana zaplanowana zmiana KOŃCZYŁA poprzedni limit, więc ten zostaje ponownie otwarty
    /// (<see cref="BudgetItem.Reopen"/>) — bez tego kategoria straciłaby limit od miesiąca, w którym miała wejść zmiana.</item>
    /// </list>
    /// </remarks>
    public async Task<LimitSavedResponseDto> HandleAsync(SetLimitRequestDto request, CancellationToken ct)
    {
        if (request.Amount <= 0) throw new LimitAmountInvalidException();
        if (request.WarningThreshold is < LimitWarning.MinThreshold or > LimitWarning.MaxThreshold)
        {
            throw new LimitWarningThresholdInvalidException();
        }

        var budget = await scope.SingleAsync(request.BudgetId, ct);
        var category = (await limitCategories.AllowedAsync(ct)).FirstOrDefault(c => c.BusinessId == request.CategoryId)
            ?? throw new LimitCategoryInvalidException();

        var currentMonth = scope.CurrentMonth();
        var validFrom = new DateOnly(request.ValidFrom.Year, request.ValidFrom.Month, 1);

        var history = await db.BudgetItems
            .Where(i => i.BudgetBusinessId == budget && i.CategoryId == category.Id)
            .OrderBy(i => i.ValidFrom).ThenBy(i => i.Id)
            .ToListAsync(ct);

        if (history.Count > 0 && validFrom < currentMonth) throw new LimitHistoryLockedException();

        var scheduled = history.Where(i => i.ValidFrom > validFrom).ToList();
        db.BudgetItems.RemoveRange(scheduled);

        var latest = history.LastOrDefault(i => i.ValidFrom <= validFrom);

        if (latest is not null && scheduled.Count > 0 && latest.ValidTo == scheduled[0].ValidFrom.AddMonths(-1))
        {
            latest.Reopen();
        }

        if (latest is not null && latest.AppliesTo(validFrom))
        {
            if (latest.Limit == request.Amount && latest.WarningThreshold == request.WarningThreshold)
            {
                await db.SaveChangesAsync(ct);
                return Saved(latest);
            }

            if (latest.ValidFrom == validFrom)
            {
                latest.Correct(request.Amount, request.WarningThreshold);
                await db.SaveChangesAsync(ct);
                return Saved(latest);
            }

            latest.End(validFrom.AddMonths(-1));
        }

        var item = new BudgetItem(
            budget, category.Id, request.Amount, validFrom, request.WarningThreshold, currentUser.UserId ?? default);
        db.BudgetItems.Add(item);
        await db.SaveChangesAsync(ct);

        return Saved(item);
    }

    private static LimitSavedResponseDto Saved(BudgetItem item) =>
        new(item.BusinessId, item.Limit, item.WarningThreshold, item.ValidFrom);
}
