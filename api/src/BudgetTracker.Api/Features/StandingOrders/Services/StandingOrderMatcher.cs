using System.Linq.Expressions;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Services;

/// <summary>
/// Przypinanie transakcji do zleceń stałych według ich reguł — wstecz (po zapisie zlecenia) i dla nowego importu.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ Przypięcie NIE zmienia kategorii — zmienia wyłącznie <see cref="Transaction.StandingOrderBusinessId"/>.</item>
/// <item>Pasują tylko WYDATKI (kwota ujemna) z budżetu zlecenia, spełniające KTÓRĄKOLWIEK regułę: opis ZAWIERA
/// frazę (bez wielkości liter), a kwota bezwzględna mieści się w zakresie tej samej reguły.</item>
/// <item>Zakończone zlecenie łapie tylko transakcje do końca ostatniego miesiąca — po nim import już nie przypina.</item>
/// <item>Transakcja należy do JEDNEGO zlecenia. Już przypiętej nie przejmuje inne zlecenie — wygrywa to, które było
/// pierwsze; podgląd reguł mówi, ile pasujących jest zajętych.</item>
/// <item>⚠️ Ręczne odpięcie (<see cref="Transaction.StandingOrderUnpinnedFrom"/>) jest respektowane przy KAŻDYM
/// dopasowaniu — inaczej „Odepnij” działałoby do pierwszej zmiany reguły.</item>
/// <item><c>ExecuteUpdate</c>, bo przypięcia wstecz dotyczą całej historii budżetu — materializowanie tysiąca
/// transakcji, żeby ustawić jedno pole, byłoby marnotrawstwem. Wewnątrz transakcji bazodanowej wywołującego.</item>
/// </list>
/// </remarks>
public sealed class StandingOrderMatcher(AppDbContext db)
{
    /// <summary>Znak ucieczki dla <c>ILIKE</c> — musi być podany jawnie, Postgres nie zakłada żadnego.</summary>
    private const string LikeEscapeChar = "\\";

    /// <summary>Przelicza przypięcia jednego zlecenia od zera: zdejmuje dotychczasowe i przypina według aktualnych reguł.</summary>
    /// <returns>Liczba transakcji przypiętych po przeliczeniu.</returns>
    public async Task<int> RematchAsync(StandingOrder order, CancellationToken ct)
    {
        await UnpinAllAsync(order.BusinessId, ct);

        return await Candidates(order.BudgetBusinessId, order.BusinessId, order.Rules, order.EndMonth)
            .Where(t => t.StandingOrderBusinessId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.StandingOrderBusinessId, order.BusinessId), ct);
    }

    /// <summary>Przypina świeżo zaimportowane transakcje do zleceń budżetu — w kolejności zakładania zleceń.</summary>
    public async Task PinAsync(Guid budgetBusinessId, IReadOnlyCollection<Guid> transactionBusinessIds, CancellationToken ct)
    {
        if (transactionBusinessIds.Count == 0) return;

        var orders = await db.StandingOrders
            .Where(o => o.BudgetBusinessId == budgetBusinessId)
            .OrderBy(o => o.Id)
            .ToListAsync(ct);

        foreach (var order in orders)
        {
            await Candidates(budgetBusinessId, order.BusinessId, order.Rules, order.EndMonth)
                .Where(t => t.StandingOrderBusinessId == null && transactionBusinessIds.Contains(t.BusinessId))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.StandingOrderBusinessId, order.BusinessId), ct);
        }
    }

    /// <summary>Zdejmuje wszystkie przypięcia zlecenia i pamięć o ręcznych odpięciach od niego (przy usunięciu zlecenia).</summary>
    public async Task ForgetAsync(Guid standingOrderBusinessId, CancellationToken ct)
    {
        await UnpinAllAsync(standingOrderBusinessId, ct);

        await db.Transactions
            .Where(t => t.StandingOrderUnpinnedFrom == standingOrderBusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.StandingOrderUnpinnedFrom, (Guid?)null), ct);
    }

    /// <summary>
    /// Wydatki budżetu pasujące do którejkolwiek reguły, z pominięciem ręcznie odpiętych od
    /// <paramref name="standingOrderBusinessId"/> i — przy zakończonym zleceniu — późniejszych niż jego ostatni miesiąc.
    /// </summary>
    /// <remarks>Bez warunku na obecne przypięcie — wywołujący decyduje, czy zajęte przez inne zlecenie mają odpaść.</remarks>
    public IQueryable<Transaction> Candidates(
        Guid budgetBusinessId, Guid? standingOrderBusinessId, IReadOnlyCollection<StandingOrderRule> rules, DateOnly? endMonth)
    {
        var query = db.Transactions.Where(t =>
            t.BudgetBusinessId == budgetBusinessId
            && t.Amount < 0
            && (standingOrderBusinessId == null || t.StandingOrderUnpinnedFrom == null
                || t.StandingOrderUnpinnedFrom != standingOrderBusinessId));

        if (endMonth is { } end)
        {
            var afterEnd = end.AddMonths(1);
            query = query.Where(t => t.Date < afterEnd);
        }

        return query.Where(AnyRule(rules));
    }

    /// <summary>„Lub” po regułach, złożone w jedno drzewo wyrażeń — EF tłumaczy je na jeden <c>WHERE (…) OR (…)</c>.</summary>
    /// <remarks>
    /// Każda reguła trafia do zapytania przez domknięcie, więc fraza i kwoty idą jako PARAMETRY, nie literały SQL.
    /// Pusta lista daje fałsz — zlecenie bez reguł niczego nie łapie (walidacja i tak na to nie pozwala).
    /// </remarks>
    private static Expression<Func<Transaction, bool>> AnyRule(IReadOnlyCollection<StandingOrderRule> rules)
    {
        var parameter = Expression.Parameter(typeof(Transaction), "t");
        Expression body = Expression.Constant(false);

        foreach (var rule in rules)
        {
            var like = $"%{EscapeLikePattern(rule.TitlePattern)}%";
            var from = rule.AmountFrom;
            var to = rule.AmountTo;
            Expression<Func<Transaction, bool>> one = t =>
                -t.Amount >= from
                && -t.Amount <= to
                && EF.Functions.ILike(t.Description, like, LikeEscapeChar);

            var rebound = new ParameterSwap(one.Parameters[0], parameter).Visit(one.Body);
            body = body is ConstantExpression ? rebound : Expression.OrElse(body, rebound);
        }

        return Expression.Lambda<Func<Transaction, bool>>(body, parameter);
    }

    private Task<int> UnpinAllAsync(Guid standingOrderBusinessId, CancellationToken ct) =>
        db.Transactions
            .Where(t => t.StandingOrderBusinessId == standingOrderBusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.StandingOrderBusinessId, (Guid?)null), ct);

    /// <summary>Fraza reguły jest DANYMI, nie wzorcem — „5%” w tytule ma znaczyć „5%”, a nie „zawiera 5”.</summary>
    /// <remarks>Backslash pierwszy, inaczej podwoiłby ucieczki dopisane w kolejnych krokach.</remarks>
    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    /// <summary>Podmienia parametr lambdy jednej reguły na wspólny parametr złożonego warunku.</summary>
    private sealed class ParameterSwap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
