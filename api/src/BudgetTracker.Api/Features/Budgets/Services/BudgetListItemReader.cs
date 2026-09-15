using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Consts;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>
/// Buduje wiersze listy budżetów w ustawieniach. Wspólne dla zapytania o listę i dla komend,
/// które po zmianie oddają zaktualizowany wiersz.
/// </summary>
/// <remarks>
/// Lista widzi TAKŻE usunięte (<c>IgnoreQueryFilters</c>) — to jedyne miejsce w aplikacji, gdzie
/// usunięty budżet jest widoczny, bo tylko tu da się go przywrócić.
/// </remarks>
public sealed class BudgetListItemReader(AppDbContext db, TimeProvider clock)
{
    /// <summary>Wszystkie budżety, od najnowszego. Trzy zapytania łącznie, a nie trzy na budżet.</summary>
    /// <remarks>
    /// ⚠️ Miesięczny limit to suma limitów obowiązujących w BIEŻĄCYM miesiącu. Limit ma historię
    /// (<see cref="BudgetItem.ValidFrom"/>), więc suma wszystkich wierszy liczyłaby każdą zmianę kwoty
    /// jako osobny limit — po jednej podwyżce „miesięczny limit" wyszedłby prawie dwa razy za duży.
    /// </remarks>
    public async Task<IReadOnlyList<BudgetListItemResponseDto>> ReadAllAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);

        var budgets = await db.Budgets
            .IgnoreQueryFilters()
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .ToListAsync(ct);

        var budgetIds = budgets.Select(b => b.BusinessId).ToList();

        var sums = await db.Transactions
            .Where(t => t.BudgetBusinessId != null && budgetIds.Contains(t.BudgetBusinessId.Value))
            .GroupBy(t => t.BudgetBusinessId!.Value)
            .Select(g => new { BudgetBusinessId = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToDictionaryAsync(x => x.BudgetBusinessId, ct);

        var limits = await db.BudgetItems
            .Where(i => budgetIds.Contains(i.BudgetBusinessId)
                        && i.ValidFrom <= currentMonth && (i.ValidTo == null || i.ValidTo >= currentMonth))
            .GroupBy(i => i.BudgetBusinessId)
            .Select(g => new { BudgetBusinessId = g.Key, Total = g.Sum(i => i.Limit) })
            .ToDictionaryAsync(x => x.BudgetBusinessId, x => x.Total, ct);

        return budgets.Select(b => new BudgetListItemResponseDto(
            Id: b.BusinessId,
            Name: b.Name,
            Month: b.Month,
            Currency: b.Currency,
            InitialBalance: b.InitialBalance,
            Balance: b.InitialBalance + (sums.TryGetValue(b.BusinessId, out var s) ? s.Total : 0m),
            MonthlyLimit: limits.GetValueOrDefault(b.BusinessId, 0m),
            CreatedAt: b.CreatedAt,
            TransactionCount: sums.TryGetValue(b.BusinessId, out var c) ? c.Count : 0,
            Status: StatusOf(b),
            DisabledAt: b.DisabledAt,
            DeletedAt: b.DeletedAt,
            LinkedSavingsBudgetId: b.LinkedSavingsBudgetBusinessId,
            SavingsTransferRules: [.. b.SavingsTransferRules.Select(r =>
                new TitleAmountRuleResponseDto(r.TitlePattern, r.AmountFrom, r.AmountTo))])).ToList();
    }

    /// <summary>Jeden wiersz — ten sam kształt co na liście, więc front może podmienić go w miejscu.</summary>
    public async Task<BudgetListItemResponseDto> ReadOneAsync(Guid businessId, CancellationToken ct) =>
        (await ReadAllAsync(ct)).First(b => b.Id == businessId);

    /// <summary>
    /// Skasowany wygrywa nad wyłączonym: budżet może być jednym i drugim naraz (usunięcie
    /// nie włącza go z powrotem), a użytkownika interesuje wtedy to, że go nie ma.
    /// </summary>
    private static BudgetStatus StatusOf(Budget b) => b switch
    {
        { DeletedAt: not null } => BudgetStatus.Deleted,
        { DisabledAt: not null } => BudgetStatus.Disabled,
        _ => BudgetStatus.Active,
    };
}
