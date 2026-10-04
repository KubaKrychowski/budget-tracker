using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Queries;

/// <summary>Podgląd okna „Zastosuj w budżecie”: akcje „do budżetu” strategii razem z tym, co by się z nimi stało.</summary>
public sealed class GetStrategyApplyPreviewQueryHandler(
    AppDbContext db, StrategyApplyPlanner planner, StrategiesBudgetScope scope)
{
    public async Task<StrategyApplyPreviewResponseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.BusinessId == id, ct)
            ?? throw new StrategyNotFoundException(id);
        var budgetName = await db.Budgets
            .Where(b => b.BusinessId == strategy.BudgetBusinessId)
            .Select(b => b.Name)
            .SingleOrDefaultAsync(ct) ?? string.Empty;

        var plan = await planner.PlanAsync(strategy, scope.CurrentMonth(), ct);
        return new StrategyApplyPreviewResponseDto(
            strategy.BudgetBusinessId,
            budgetName,
            [.. plan.Select(p => new StrategyApplyItemResponseDto(
                p.Node.Id,
                p.Node.Type,
                p.Node.Title,
                p.Node.Amount,
                p.Status,
                p.Node.Type is StrategyNodeType.CreateReservation or StrategyNodeType.EndStandingOrder
                    ? p.Node.Month
                    : p.ApplyMonth,
                p.CurrentAmount))]);
    }
}
