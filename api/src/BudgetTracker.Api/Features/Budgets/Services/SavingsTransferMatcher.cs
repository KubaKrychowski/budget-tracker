using System.Linq.Expressions;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>
/// Przypinanie własnych transakcji budżetu jako transferu do/z powiązanego budżetu oszczędnościowego,
/// według reguł budżetu (<see cref="Budget.SavingsTransferRules"/>) — wstecz i dla nowego importu.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Ten sam mechanizm co <c>StandingOrderMatcher</c>, uproszczony: reguły są WŁASNOŚCIĄ JEDNEGO
/// budżetu, nie wielu bytów per budżet — nie ma tu rywalizacji „kto był pierwszy".</item>
/// <item>⚠️ W odróżnieniu od zleceń stałych dopasowanie NIE zakłada znaku kwoty — transfer do budżetu
/// oszczędnościowego jest wydatkiem, powrotny przychodem — więc działa na WARTOŚCI BEZWZGLĘDNEJ.</item>
/// <item>Przypięcie NIE zmienia kategorii ani opisu — historia zostaje 1:1 z bankiem.</item>
/// <item>⚠️ Ręczne odpięcie (<see cref="Transaction.SavingsTransferUnpinnedFrom"/>) jest respektowane
/// przy KAŻDYM dopasowaniu — inaczej „Odepnij" działałoby do pierwszej zmiany reguły.</item>
/// <item><c>ExecuteUpdate</c>, bo przypięcia wstecz dotyczą całej historii budżetu — materializowanie
/// tysiąca transakcji, żeby ustawić jedno pole, byłoby marnotrawstwem. Wewnątrz transakcji bazodanowej
/// wywołującego.</item>
/// </list>
/// </remarks>
public sealed class SavingsTransferMatcher(AppDbContext db)
{
    /// <summary>Przelicza przypięcia budżetu od zera: zdejmuje dotychczasowe i przypina według aktualnych reguł.</summary>
    /// <returns>Liczba transakcji przypiętych po przeliczeniu. Zero, gdy budżet nie ma powiązania.</returns>
    public async Task<int> RematchAsync(Budget budget, CancellationToken ct)
    {
        if (budget.LinkedSavingsBudgetBusinessId is not { } linked) return 0;

        await UnpinAllAsync(budget.BusinessId, linked, ct);

        return await Candidates(budget.BusinessId, linked, budget.SavingsTransferRules)
            .Where(t => t.SavingsTransferBudgetBusinessId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SavingsTransferBudgetBusinessId, linked), ct);
    }

    /// <summary>Przypina świeżo zaimportowane transakcje budżetu według jego reguł transferu.</summary>
    public async Task PinAsync(Budget budget, IReadOnlyCollection<Guid> transactionBusinessIds, CancellationToken ct)
    {
        if (transactionBusinessIds.Count == 0) return;
        if (budget.LinkedSavingsBudgetBusinessId is not { } linked) return;

        await Candidates(budget.BusinessId, linked, budget.SavingsTransferRules)
            .Where(t => t.SavingsTransferBudgetBusinessId == null && transactionBusinessIds.Contains(t.BusinessId))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SavingsTransferBudgetBusinessId, linked), ct);
    }

    /// <summary>Zdejmuje wszystkie przypięcia do <paramref name="linkedBudgetBusinessId"/> i pamięć ręcznych odpięć od niego.</summary>
    /// <remarks>Wołane, gdy powiązanie znika: budżet je zdejmuje albo powiązany budżet oszczędnościowy zostaje usunięty.</remarks>
    public async Task ForgetAsync(Guid mainBudgetBusinessId, Guid linkedBudgetBusinessId, CancellationToken ct)
    {
        await UnpinAllAsync(mainBudgetBusinessId, linkedBudgetBusinessId, ct);

        await db.Transactions
            .Where(t => t.BudgetBusinessId == mainBudgetBusinessId && t.SavingsTransferUnpinnedFrom == linkedBudgetBusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SavingsTransferUnpinnedFrom, (Guid?)null), ct);
    }

    /// <summary>
    /// Własne transakcje budżetu pasujące do którejkolwiek reguły, z pominięciem ręcznie odpiętych
    /// od <paramref name="linkedBudgetBusinessId"/>.
    /// </summary>
    /// <remarks>Bez warunku na obecne przypięcie — wywołujący decyduje, czy już przypięte mają odpaść.</remarks>
    public IQueryable<Transaction> Candidates(
        Guid budgetBusinessId, Guid linkedBudgetBusinessId, IReadOnlyCollection<TitleAmountRule> rules)
    {
        var query = db.Transactions.Where(t =>
            t.BudgetBusinessId == budgetBusinessId
            && (t.SavingsTransferUnpinnedFrom == null || t.SavingsTransferUnpinnedFrom != linkedBudgetBusinessId));

        return query.Where(AnyRule(rules));
    }

    /// <summary>„Lub” po regułach, złożone w jedno drzewo wyrażeń — EF tłumaczy je na jeden <c>WHERE (…) OR (…)</c>.</summary>
    /// <remarks>
    /// Każda reguła trafia do zapytania przez domknięcie, więc fraza i kwoty idą jako PARAMETRY, nie literały SQL.
    /// Pusta lista daje fałsz — budżet bez reguł niczego nie łapie.
    /// </remarks>
    private static Expression<Func<Transaction, bool>> AnyRule(IReadOnlyCollection<TitleAmountRule> rules)
    {
        var parameter = Expression.Parameter(typeof(Transaction), "t");
        Expression body = Expression.Constant(false);

        foreach (var rule in rules)
        {
            var like = LikePattern.Contains(rule.TitlePattern);
            var from = rule.AmountFrom;
            var to = rule.AmountTo;
            Expression<Func<Transaction, bool>> one = t =>
                Math.Abs(t.Amount) >= from
                && Math.Abs(t.Amount) <= to
                && EF.Functions.ILike(t.Description, like, LikePattern.EscapeChar);

            var rebound = new ParameterSwap(one.Parameters[0], parameter).Visit(one.Body);
            body = body is ConstantExpression ? rebound : Expression.OrElse(body, rebound);
        }

        return Expression.Lambda<Func<Transaction, bool>>(body, parameter);
    }

    private Task<int> UnpinAllAsync(Guid mainBudgetBusinessId, Guid linkedBudgetBusinessId, CancellationToken ct) =>
        db.Transactions
            .Where(t => t.BudgetBusinessId == mainBudgetBusinessId && t.SavingsTransferBudgetBusinessId == linkedBudgetBusinessId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SavingsTransferBudgetBusinessId, (Guid?)null), ct);

    /// <summary>Podmienia parametr lambdy jednej reguły na wspólny parametr złożonego warunku.</summary>
    private sealed class ParameterSwap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
