using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.EpisodicOrders.Commands;
using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.Limits.Commands;
using BudgetTracker.Api.Features.Limits.Contracts;
using BudgetTracker.Api.Features.Savings.Commands;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Commands;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Models;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Commands;

/// <summary>
/// Zakłada w budżecie to, co strategia planuje: woła te same handlery co ekrany celów, rezerwacji, limitów, zleceń
/// stałych i epizodycznych — dzięki temu obowiązują tu wszystkie ich reguły (np. zamknięta historia limitów).
/// </summary>
/// <remarks>
/// ⚠️ Zastosowanie jest atomowe: endpoint stoi w transakcji żądania (<c>RlsTransactionEndpointFilter</c>), więc błąd
/// jednej akcji wycofuje wszystkie. Stan akcji liczy się od nowa w chwili zastosowania, a nie z okna sprzed minuty —
/// zaznaczona akcja, która w międzyczasie „już jest”, jest pomijana, nie dubluje się.
/// </remarks>
public sealed class ApplyStrategyCommandHandler(
    AppDbContext db,
    StrategyApplyPlanner planner,
    StrategiesBudgetScope scope,
    SetSavingsGoalCommandHandler setGoal,
    CreateSavingsReservationCommandHandler createReservation,
    SetLimitCommandHandler setLimit,
    EndStandingOrderCommandHandler endOrder,
    CreateEpisodicOrderCommandHandler createEpisodicOrder)
{
    public async Task<ApplyStrategyResponseDto> HandleAsync(Guid id, ApplyStrategyRequestDto request, CancellationToken ct)
    {
        var selected = (request.NodeIds ?? []).ToHashSet();
        if (selected.Count == 0) throw new StrategyApplyNothingSelectedException();

        var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.BusinessId == id, ct)
            ?? throw new StrategyNotFoundException(id);
        var budget = strategy.BudgetBusinessId;

        var applied = new List<string>();
        var skipped = new List<string>();
        foreach (var item in await planner.PlanAsync(strategy, scope.CurrentMonth(), request.VariantId, ct))
        {
            if (!selected.Remove(item.Node.Id)) continue;
            if (item.Status is not (StrategyApplyStatus.New or StrategyApplyStatus.Change))
            {
                skipped.Add(item.Node.Id);
                continue;
            }

            await ApplyAsync(budget, item, ct);
            applied.Add(item.Node.Id);
        }

        skipped.AddRange(selected);
        return new ApplyStrategyResponseDto(applied, skipped);
    }

    private async Task ApplyAsync(Guid budget, StrategyApplyPlanItem item, CancellationToken ct)
    {
        var node = item.Node;
        var amount = node.Amount ?? 0m;
        switch (node.Type)
        {
            case StrategyNodeType.SetSavingsGoal:
                await setGoal.HandleAsync(new SetSavingsGoalRequestDto(amount, budget), ct);
                break;
            case StrategyNodeType.CreateReservation:
                await createReservation.HandleAsync(
                    new SaveReservationRequestDto(node.Title, amount, node.Month, 0, budget), ct);
                break;
            case StrategyNodeType.SetLimit:
                await setLimit.HandleAsync(
                    new SetLimitRequestDto(
                        budget, node.CategoryId!.Value, amount,
                        (int)(node.Threshold ?? StrategyApplyPlanner.DefaultWarningThreshold), item.ApplyMonth),
                    ct);
                break;
            case StrategyNodeType.EndStandingOrder:
                await endOrder.EndAsync(
                    node.StandingOrderId!.Value, new EndStandingOrderRequestDto(node.Month!.Value), ct);
                break;
            default:
                await createEpisodicOrder.HandleAsync(
                    new SaveEpisodicOrderRequestDto(
                        budget, node.Title, null, null, node.CategoryId, amount, item.ApplyMonth),
                    ct);
                break;
        }
    }
}
