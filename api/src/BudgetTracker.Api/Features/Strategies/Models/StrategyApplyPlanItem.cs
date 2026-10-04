using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Strategies.Consts;

namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Zaplanowane zastosowanie jednej akcji „do budżetu” — wejście dla podglądu i dla samego zastosowania.</summary>
/// <param name="Node">Kafelek akcji.</param>
/// <param name="Status">Co by się stało.</param>
/// <param name="ApplyMonth">Miesiąc, w którym akcja zadziała w budżecie: nie wcześniej niż bieżący (historia jest zamknięta).</param>
/// <param name="CurrentAmount">Kwota, którą akcja zastąpi.</param>
public sealed record StrategyApplyPlanItem(
    StrategyNode Node, StrategyApplyStatus Status, DateOnly ApplyMonth, decimal? CurrentAmount);
