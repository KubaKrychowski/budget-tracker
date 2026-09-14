using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Queries;

/// <summary>Podgląd reguł zlecenia przed zapisem — nic nie zapisuje.</summary>
public sealed class PreviewStandingOrderQueryHandler(
    AppDbContext db, StandingOrdersBudgetScope scope, StandingOrderMatcher matcher)
{
    /// <summary>Ile wydatków z historii budżetu pasuje do reguł, ile z nich zajmuje inne zlecenie, i ostatni z nich.</summary>
    /// <remarks>
    /// Liczy CAŁĄ historię (a przy zakończonym zleceniu — do ostatniego miesiąca), bo zapis przypina wstecz — podgląd
    /// ma pokazać dokładnie to, co zrobi zapis. Transakcja pasująca do dwóch reguł liczy się raz.
    /// </remarks>
    public async Task<StandingOrderPreviewResponseDto> HandleAsync(StandingOrderPreviewRequestDto request, CancellationToken ct)
    {
        var rules = StandingOrderRuleValidator.ValidateRules(request.Rules);
        var budget = await scope.SingleAsync(request.BudgetId, ct);

        var endMonth = request.StandingOrderId is { } id
            ? await db.StandingOrders.Where(o => o.BusinessId == id).Select(o => o.EndMonth).FirstOrDefaultAsync(ct)
            : null;
        var matching = matcher.Candidates(budget, request.StandingOrderId, rules, endMonth);

        var count = await matching.CountAsync(ct);
        var taken = await matching
            .CountAsync(t => t.StandingOrderBusinessId != null && t.StandingOrderBusinessId != request.StandingOrderId, ct);
        var last = await matching
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Select(t => new { t.Date, Amount = -t.Amount })
            .FirstOrDefaultAsync(ct);

        return new StandingOrderPreviewResponseDto(count, taken, last?.Date, last?.Amount);
    }
}
