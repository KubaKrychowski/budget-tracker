using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.EpisodicOrders.Queries;

/// <summary>Wydatki do wskazania w „Oznacz jako kupione…” i w modalu „Już zrealizowane”.</summary>
/// <remarks>
/// <para>
/// Przy kupieniu zaplanowanego lista zaczyna się 2 miesiące przed terminem — zakup „przed czasem” jest normalny,
/// a starsze wydatki tylko zasłaniałyby właściwy. Po terminie brak górnej granicy: rzeczy kupuje się też później.
/// Zlecenie bez terminu nie ma też dolnej granicy — szukajka zawęża resztę.
/// </para>
/// <para>Od najnowszego, bo szuka się zwykle czegoś, co zeszło niedawno; szukajka zawęża po tytule.</para>
/// </remarks>
public sealed class GetEpisodicOrderCandidatesQueryHandler(
    AppDbContext db, EpisodicOrdersBudgetScope scope, EpisodicOrderLookup orders, EpisodicOrderTransactions transactions)
{
    private const int MaxCandidates = 50;
    private const int MonthsBeforeDue = 2;

    public async Task<IReadOnlyList<EpisodicOrderCandidateResponseDto>> HandleAsync(
        Guid? budgetId, Guid? orderId, string? search, CancellationToken ct)
    {
        Guid budget;
        DateOnly? from = null;
        if (orderId is { } id)
        {
            var order = await orders.FindAsync(id, ct);
            budget = order.BudgetBusinessId;
            from = order.DueMonth?.AddMonths(-MonthsBeforeDue);
        }
        else
        {
            budget = await scope.SingleAsync(budgetId, ct);
        }

        var query = transactions.Usable(budget, orderId);
        if (from is { } start) query = query.Where(t => t.Date >= start);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(t => EF.Functions.ILike(t.Description, pattern, LikePattern.EscapeChar));
        }

        return await query
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Take(MaxCandidates)
            .Select(t => new EpisodicOrderCandidateResponseDto(
                t.BusinessId, t.Date, t.Description, t.Amount,
                db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefault()))
            .ToListAsync(ct);
    }
}
