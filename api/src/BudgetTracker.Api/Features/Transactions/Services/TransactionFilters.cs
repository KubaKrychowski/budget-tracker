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
    /// <summary>Znak ucieczki dla <c>ILIKE</c> — musi być podany jawnie, Postgres nie zakłada żadnego.</summary>
    private const string LikeEscapeChar = "\\";

    /// <summary>Zawęża <paramref name="query"/> do wierszy pasujących do <paramref name="filter"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Filtr kategorii porównuje publiczny <c>BusinessId</c> podzapytaniem po <c>Categories</c> —
    /// transakcja nie ma nawigacji do kategorii (CLAUDE.md §5).</item>
    /// <item>Zakres „od-do" działa na WARTOŚCI BEZWZGLĘDNEJ — użytkownik wpisuje kwotę bez znaku,
    /// tak jak ją widzi w kolumnie, niezależnie od kierunku transakcji.</item>
    /// <item>Szukana fraza jest DANYMI, nie wzorcem — patrz <see cref="EscapeLikePattern"/>.</item>
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

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{EscapeLikePattern(filter.Search)}%";
            query = query.Where(t => EF.Functions.ILike(t.Description, pattern, LikeEscapeChar));
        }

        return query;
    }

    /// <summary>
    /// Neutralizuje metaznaki LIKE w tekście od użytkownika.
    /// </summary>
    /// <remarks>
    /// Bez tego szukanie „5%" (np. stawki podatku w opisie) zamienia się we wzorzec <c>%5%%</c>, czyli po prostu
    /// „zawiera 5" — i wyszukiwarka zwraca „ODSETKI 15 PLN" jako trafienie. To nie jest dziura na wstrzyknięcie
    /// SQL (zapytanie i tak leci parametrem), tylko cicho błędne wyniki.
    ///
    /// Backslash pierwszy, inaczej podwoiłby ucieczki dopisane w kolejnych krokach.
    /// </remarks>
    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
}
