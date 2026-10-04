using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Limits.Services;
using BudgetTracker.Api.Features.Strategies.Consts;
using BudgetTracker.Api.Features.Strategies.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Services;

/// <summary>
/// Dla każdej akcji „do budżetu” (cel, rezerwacja, limit, zlecenie stałe, wydatek jednorazowy) ustala, co by się
/// stało, gdyby strategię zastosować teraz — i robi to tą samą drogą dla podglądu i dla samego zastosowania.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Akcja czeka, dopóki jej miesiąc w symulacji nie nadejdzie (<see cref="StrategyApplyStatus.Waiting"/>): „załóż
/// limit po spłacie kredytu” nie ma sensu na dziś. Miesiąc wykonania pochodzi z SYMULACJI, nie z kafelka, więc zmiana
/// kwoty przesuwającej spłatę kredytu przesuwa też moment, w którym akcja przestaje czekać.
/// </para>
/// <para>
/// „Już jest” rozstrzyga porównanie z tym, co w budżecie obowiązuje — dzięki temu dwukrotne zastosowanie niczego nie
/// zdubluje. Ta sama kwota celu albo limitu to nie zmiana; rezerwacja i zlecenie epizodyczne rozpoznawane są po nazwie,
/// kwocie (i terminie), zlecenie stałe po tym, że jest już zakończone nie później niż w wybranym miesiącu.
/// </para>
/// </remarks>
public sealed class StrategyApplyPlanner(AppDbContext db, LimitCategories limitCategories)
{
    /// <summary>Domyślny próg ostrzeżenia limitu (procent), gdy kafelek go nie podaje.</summary>
    public const int DefaultWarningThreshold = 80;

    private static readonly StrategyNodeType[] BudgetActions =
    [
        StrategyNodeType.SetSavingsGoal, StrategyNodeType.CreateReservation, StrategyNodeType.SetLimit,
        StrategyNodeType.EndStandingOrder, StrategyNodeType.CreateEpisodicOrder,
    ];

    public async Task<IReadOnlyList<StrategyApplyPlanItem>> PlanAsync(
        Strategy strategy, DateOnly currentMonth, CancellationToken ct)
    {
        var result = StrategySimulator.Run(StrategyMapping.ToInput(strategy));
        var firedIn = result.Nodes.ToDictionary(n => n.NodeId, n => n.FiredIn);
        var withProblems = result.Problems.Select(p => p.NodeId).ToHashSet();
        var categories = (await limitCategories.AllowedAsync(ct)).ToDictionary(c => c.BusinessId, c => c.Id);

        var items = new List<StrategyApplyPlanItem>();
        foreach (var node in strategy.Nodes.Where(n => BudgetActions.Contains(n.Type)))
        {
            if (withProblems.Contains(node.Id))
            {
                items.Add(new StrategyApplyPlanItem(node, StrategyApplyStatus.Incomplete, currentMonth, null));
                continue;
            }

            if (!firedIn.TryGetValue(node.Id, out var fired) || fired is null || fired > currentMonth)
            {
                items.Add(new StrategyApplyPlanItem(node, StrategyApplyStatus.Waiting, fired ?? currentMonth, null));
                continue;
            }

            var (status, current) = await ClassifyAsync(strategy.BudgetBusinessId, node, currentMonth, categories, ct);
            items.Add(new StrategyApplyPlanItem(node, status, currentMonth, current));
        }

        return items;
    }

    /// <remarks>
    /// Do tej pory dotarła tylko akcja, która wykonała się nie później niż w bieżącym miesiącu, więc miesiąc jej
    /// zastosowania to zawsze bieżący — wcześniejszy zamykałby historię limitów (409), a późniejszy to „czeka”.
    /// </remarks>
    private async Task<(StrategyApplyStatus Status, decimal? Current)> ClassifyAsync(
        Guid budget, StrategyNode node, DateOnly applyMonth, IReadOnlyDictionary<Guid, int> categories, CancellationToken ct)
    {
        var amount = node.Amount ?? 0m;
        switch (node.Type)
        {
            case StrategyNodeType.SetSavingsGoal:
            {
                var goal = await db.SavingsGoals
                    .Where(g => g.BudgetBusinessId == budget && g.EndedOn == null)
                    .OrderByDescending(g => g.StartedOn).ThenByDescending(g => g.Id)
                    .FirstOrDefaultAsync(ct);
                return goal is null ? (StrategyApplyStatus.New, null)
                    : goal.Amount == amount ? (StrategyApplyStatus.Exists, goal.Amount)
                    : (StrategyApplyStatus.Change, goal.Amount);
            }

            case StrategyNodeType.CreateReservation:
            {
                var name = node.Title.Trim().ToLower();
                var exists = await db.SavingsReservations.AnyAsync(
                    r => r.BudgetBusinessId == budget && r.SettledAt == null && r.Amount == amount
                         && r.Name.ToLower() == name, ct);
                return (exists ? StrategyApplyStatus.Exists : StrategyApplyStatus.New, null);
            }

            case StrategyNodeType.SetLimit:
            {
                if (!categories.TryGetValue(node.CategoryId!.Value, out var categoryId))
                {
                    return (StrategyApplyStatus.Incomplete, null);
                }

                var threshold = (int)(node.Threshold ?? DefaultWarningThreshold);
                var active = await db.BudgetItems
                    .Where(i => i.BudgetBusinessId == budget && i.CategoryId == categoryId && i.ValidFrom <= applyMonth
                                && (i.ValidTo == null || i.ValidTo >= applyMonth))
                    .OrderByDescending(i => i.ValidFrom).FirstOrDefaultAsync(ct);
                return active is null ? (StrategyApplyStatus.New, null)
                    : active.Limit == amount && active.WarningThreshold == threshold ? (StrategyApplyStatus.Exists, active.Limit)
                    : (StrategyApplyStatus.Change, active.Limit);
            }

            case StrategyNodeType.EndStandingOrder:
            {
                var orderId = node.StandingOrderId!.Value;
                var order = await db.StandingOrders.FirstOrDefaultAsync(
                    o => o.BudgetBusinessId == budget && o.BusinessId == orderId, ct);
                if (order is null) return (StrategyApplyStatus.Incomplete, null);
                var last = new DateOnly(node.Month!.Value.Year, node.Month.Value.Month, 1);
                return order.EndMonth is { } end && end <= last
                    ? (StrategyApplyStatus.Exists, order.ExpectedAmount)
                    : (StrategyApplyStatus.Change, order.ExpectedAmount);
            }

            default:
            {
                var name = node.Title.Trim().ToLower();
                var exists = await db.EpisodicOrders.AnyAsync(
                    o => o.BudgetBusinessId == budget && o.PlannedAmount == amount && o.DueMonth == applyMonth
                         && o.Name.ToLower() == name, ct);
                return (exists ? StrategyApplyStatus.Exists : StrategyApplyStatus.New, null);
            }
        }
    }
}
