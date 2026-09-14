using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Services;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Queries;

/// <summary>Podgląd reguły zlecenia przed zapisem — nic nie zapisuje.</summary>
public sealed class PreviewStandingOrderQueryHandler(StandingOrdersBudgetScope scope, StandingOrderMatcher matcher)
{
    /// <summary>Ile wydatków z historii budżetu pasuje do reguły, ile z nich zajmuje inne zlecenie, i ostatni z nich.</summary>
    /// <remarks>
    /// Liczy CAŁĄ historię, bo zapis przypina wstecz — podgląd ma pokazać dokładnie to, co zrobi zapis.
    /// </remarks>
    public async Task<StandingOrderPreviewResponseDto> HandleAsync(StandingOrderPreviewRequestDto request, CancellationToken ct)
    {
        var pattern = StandingOrderRuleValidator.ValidatePattern(request.TitlePattern);
        StandingOrderRuleValidator.ValidateRange(request.AmountFrom, request.AmountTo);

        var budget = await scope.SingleAsync(request.BudgetId, ct);
        var matching = matcher.Candidates(budget, request.StandingOrderId, pattern, request.AmountFrom, request.AmountTo);

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
