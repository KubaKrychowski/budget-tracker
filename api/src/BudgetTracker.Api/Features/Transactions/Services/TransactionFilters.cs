using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Services;

/// <summary>
/// Nakłada <see cref="TransactionFilterRequestDto"/> na zapytanie — jedna implementacja dla listy i dla zasięgu
/// „cały pasujący filtr" w akcjach masowych, żeby oba wejścia liczyły dokładnie ten sam zestaw wierszy.
/// </summary>
public sealed class TransactionFilters(AppDbContext db)
{
    /// <summary>Zawęża <paramref name="query"/> do wierszy pasujących do <paramref name="filter"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Filtr kategorii porównuje publiczny <c>BusinessId</c> podzapytaniem po <c>Categories</c> —
    /// transakcja nie ma nawigacji do kategorii (CLAUDE.md §5).</item>
    /// <item>Zakres „od-do" działa na WARTOŚCI BEZWZGLĘDNEJ — użytkownik wpisuje kwotę bez znaku,
    /// tak jak ją widzi w kolumnie, niezależnie od kierunku transakcji.</item>
    /// <item>Szukana fraza jest DANYMI, nie wzorcem — patrz <see cref="LikePattern.Contains"/>.</item>
    /// </list>
    /// </remarks>
    public IQueryable<Transaction> Apply(IQueryable<Transaction> query, TransactionFilterRequestDto filter)
    {
        if (filter.From is { } from) query = query.Where(t => t.Date >= from);
        if (filter.To is { } to) query = query.Where(t => t.Date <= to);

        if (filter.Uncategorized) query = query.Where(t => t.CategoryId == null);
        else if (filter.CategoryId is { } categoryId)
            query = query.Where(t => db.Categories.Any(c => c.Id == t.CategoryId && c.BusinessId == categoryId));

        query = filter.Direction switch
        {
            TransactionDirection.Expense => query.Where(t => t.Amount < 0),
            TransactionDirection.Income => query.Where(t => t.Amount > 0),
            _ => query,
        };

        if (filter.Status is { Count: > 0 } statuses)
            query = query.Where(t => statuses.Contains(t.Status));

        if (filter.AmountFrom is { } amountFrom)
            query = query.Where(t => Math.Abs(t.Amount) >= amountFrom);
        if (filter.AmountTo is { } amountTo)
            query = query.Where(t => Math.Abs(t.Amount) <= amountTo);

        if (filter.StandingOrderId is { } standingOrderId)
            query = query.Where(t => t.StandingOrderBusinessId == standingOrderId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePattern.Contains(filter.Search);
            query = query.Where(t => EF.Functions.ILike(t.Description, pattern, LikePattern.EscapeChar));
        }

        return query;
    }

    /// <summary>Nazwa zlecenia stałego z filtra — do etykiety filtra na liście; <c>null</c> bez filtra albo bez zlecenia.</summary>
    public async Task<string?> StandingOrderNameAsync(Guid? standingOrderId, CancellationToken ct) =>
        standingOrderId is { } id
            ? await db.StandingOrders.Where(o => o.BusinessId == id).Select(o => o.Name).FirstOrDefaultAsync(ct)
            : null;
}
