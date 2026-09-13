using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Limits.Consts;
using BudgetTracker.Api.Features.Limits.Contracts;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Limits.Queries;

/// <summary>Limity kategorii budżetu w jednym miesiącu i to, ile w tym miesiącu wydano.</summary>
public sealed class GetLimitsQueryHandler(AppDbContext db, LimitsBudgetScope scope, LimitCategories limitCategories)
{
    /// <summary>Ekran limitów dla budżetu (wskazanego albo domyślnego) i miesiąca (wskazanego albo bieżącego).</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Każdy miesiąc liczy się z limitem, który obowiązywał W NIM (<see cref="BudgetItem.AppliesTo"/>),
    /// a nie z dzisiejszym. Dzisiejsza kwota przyłożona do sierpnia pokazałaby przekroczenie, którego wtedy
    /// nie było — ta sama pułapka co płaska linia celu oszczędnościowego zamiast schodka.
    /// </para>
    /// <para>
    /// Wydane = suma wydatków (kwoty ujemne) z kategorią, z datą w miesiącu. Liczą się WSZYSTKIE, także
    /// oznaczone jako duże — to realnie wydane pieniądze (decyzja użytkownika). Wydatki bez kategorii
    /// nie trafiają do żadnego limitu i jadą osobno (<see cref="LimitsResponseDto.UncategorizedCount"/>).
    /// </para>
    /// </remarks>
    public async Task<LimitsResponseDto> HandleAsync(Guid? budgetId, DateOnly? month, CancellationToken ct)
    {
        var currentMonth = scope.CurrentMonth();
        var viewed = month is { } m ? new DateOnly(m.Year, m.Month, 1) : currentMonth;

        var budgets = await scope.OptionsAsync(ct);
        var selected = LimitsBudgetScope.Resolve(budgets, budgetId, currentMonth);
        var allowed = await limitCategories.AllowedAsync(ct);

        if (selected is not { } budget)
        {
            return new LimitsResponseDto(
                viewed, currentMonth, viewed < currentMonth, [], [],
                [.. allowed.Select(c => new LimitCategoryOptionResponseDto(c.BusinessId, c.Name, false))],
                0m, 0m, 0m, 0, 0, 0m, [], budgets);
        }

        var history = await db.BudgetItems
            .Where(i => i.BudgetBusinessId == budget)
            .OrderBy(i => i.ValidFrom).ThenBy(i => i.Id)
            .ToListAsync(ct);
        var applying = history.Where(i => i.AppliesTo(viewed)).ToList();

        var categoryIds = applying.Select(i => i.CategoryId).Concat(allowed.Select(c => c.Id)).ToHashSet();
        var names = await db.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.BusinessId, c.Name), ct);

        var spent = await SpentByCategoryAsync(budget, viewed, ct);

        var rows = applying
            .Select(item => Row(item, spent.GetValueOrDefault(item.CategoryId)?.Spent ?? 0m, names[item.CategoryId], history))
            .OrderBy(r => r.CategoryName)
            .ToList();

        var limitedIds = applying.Select(i => i.CategoryId).ToHashSet();
        var unlimited = allowed
            .Where(c => !limitedIds.Contains(c.Id) && spent.GetValueOrDefault(c.Id)?.Spent > 0m)
            .Select(c => new UnlimitedCategoryResponseDto(c.BusinessId, c.Name, spent[c.Id].Spent))
            .OrderByDescending(u => u.Spent).ThenBy(u => u.CategoryName)
            .ToList();

        var uncategorized = spent.GetValueOrDefault(UncategorizedKey);

        return new LimitsResponseDto(
            Month: viewed,
            CurrentMonth: currentMonth,
            ReadOnly: viewed < currentMonth,
            Limits: rows,
            Unlimited: unlimited,
            Categories: [.. allowed.Select(c => new LimitCategoryOptionResponseDto(c.BusinessId, c.Name, limitedIds.Contains(c.Id)))],
            LimitTotal: rows.Sum(r => r.Limit),
            SpentInLimited: rows.Sum(r => r.Spent),
            SpentOutside: unlimited.Sum(u => u.Spent),
            OverLimitCount: rows.Count(r => r.State == LimitState.Over),
            UncategorizedCount: uncategorized?.Count ?? 0,
            UncategorizedAmount: uncategorized?.Spent ?? 0m,
            SelectedBudgetIds: [budget],
            Budgets: budgets);
    }

    /// <summary>Klucz słownika wydatków dla transakcji bez kategorii — kategorie w bazie mają klucze dodatnie.</summary>
    private const int UncategorizedKey = 0;

    /// <summary>Wiersz tabeli: wykorzystanie, stan paska i następna zmiana kwoty, jeśli jest zaplanowana.</summary>
    /// <remarks>
    /// ⚠️ Stan liczony na kwotach, nie na zaokrąglonym procencie: 79,6% zaokrąglone do 80 zapaliłoby
    /// ostrzeżenie przy progu 80, choć próg nie został osiągnięty.
    /// </remarks>
    private static LimitRowResponseDto Row(
        BudgetItem item, decimal spent, (Guid BusinessId, string Name) category, IReadOnlyList<BudgetItem> history)
    {
        var state = spent > item.Limit ? LimitState.Over
            : spent * 100m >= item.Limit * item.WarningThreshold ? LimitState.Warning
            : LimitState.Ok;

        var next = history.FirstOrDefault(i => i.CategoryId == item.CategoryId && i.ValidFrom > item.ValidFrom);

        return new LimitRowResponseDto(
            Id: item.BusinessId,
            CategoryId: category.BusinessId,
            CategoryName: category.Name,
            Limit: item.Limit,
            Spent: spent,
            Remaining: item.Limit - spent,
            Percent: item.Limit > 0 ? (int)Math.Round(spent / item.Limit * 100m, MidpointRounding.AwayFromZero) : 0,
            WarningThreshold: item.WarningThreshold,
            State: state,
            ValidFrom: item.ValidFrom,
            NextLimit: next?.Limit,
            NextValidFrom: next?.ValidFrom);
    }

    /// <summary>Wydatki miesiąca per kategoria jednym zapytaniem — dodatnie, liczba transakcji obok.</summary>
    /// <remarks>
    /// Negacja POZA zapytaniem — EF nie tłumaczy <c>-g.Sum(...)</c> w projekcji (ta sama pułapka co w
    /// <c>GetDashboardQueryHandler</c>). Transakcje bez kategorii lądują pod <see cref="UncategorizedKey"/>.
    /// </remarks>
    private async Task<Dictionary<int, CategorySpend>> SpentByCategoryAsync(
        Guid budget, DateOnly month, CancellationToken ct)
    {
        var end = month.AddMonths(1);

        var rows = await db.Transactions
            .Where(t => t.BudgetBusinessId == budget && t.Date >= month && t.Date < end && t.Amount < 0)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key ?? UncategorizedKey, r => new CategorySpend(-r.Total, r.Count));
    }

    /// <summary>Wydane w kategorii w miesiącu, jako wartość dodatnia.</summary>
    private sealed record CategorySpend(decimal Spent, int Count);
}
