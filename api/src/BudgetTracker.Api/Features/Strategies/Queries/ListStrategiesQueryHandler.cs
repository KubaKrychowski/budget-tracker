using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Queries;

/// <summary>Ekran „Wybierz strategię” — strategie jednego budżetu, od ostatnio zmienionej.</summary>
public sealed class ListStrategiesQueryHandler(AppDbContext db, StrategiesBudgetScope scope)
{
    /// <summary>Zdarzenia to kafelki wpływu, wydatku i zdarzenia bez skutku; akcje to wszystko, co zmienia stan albo budżet.</summary>
    /// <remarks>Liczone w pamięci: węzły siedzą w kolumnie <c>jsonb</c>, a strategii w budżecie jest kilka.</remarks>
    public async Task<StrategiesResponseDto> HandleAsync(Guid? budgetId, CancellationToken ct)
    {
        var budgets = await scope.OptionsAsync(ct);
        if (StrategiesBudgetScope.Resolve(budgets, budgetId, scope.CurrentMonth()) is not { } budget)
        {
            return new StrategiesResponseDto(null, budgets, []);
        }

        var strategies = await db.Strategies
            .Where(s => s.BudgetBusinessId == budget)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);

        var rows = strategies.Select(s => new StrategyListItemResponseDto(
            s.BusinessId,
            s.Name,
            s.Nodes.Count(n => n.Type is StrategyNodeType.Trigger or StrategyNodeType.Income or StrategyNodeType.Expense),
            s.Nodes.Count(n => n.Type is >= StrategyNodeType.IncreaseSurplus and <= StrategyNodeType.SetLimit),
            s.UpdatedAt,
            s.Variants.Count));

        return new StrategiesResponseDto(budget, budgets, [.. rows]);
    }
}
