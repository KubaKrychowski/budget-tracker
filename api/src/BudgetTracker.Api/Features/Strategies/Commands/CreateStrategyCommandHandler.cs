using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Resources;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Api.Features.Strategies.Commands;

/// <summary>Zakłada strategię — pustą albo z szablonu — i od razu zwraca ją z policzonym wynikiem.</summary>
public sealed class CreateStrategyCommandHandler(
    AppDbContext db,
    StrategiesBudgetScope scope,
    ICurrentUserAccessor currentUser,
    IStringLocalizer<SharedResource> localizer)
{
    /// <summary>Horyzont nowej strategii — dwa lata wystarczają na typowy kredyt konsumpcyjny i poduszkę.</summary>
    public const int DefaultHorizonMonths = 24;

    public async Task<StrategyResponseDto> HandleAsync(CreateStrategyRequestDto request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > StrategyGraphValidator.MaxNameLength)
        {
            throw new StrategyNameRequiredException();
        }

        var budget = await scope.SingleAsync(request.BudgetId, ct);
        var start = scope.CurrentMonth();
        var graph = StrategyTemplates.Build(request.Template, start, key => localizer[key].Value);

        var strategy = new Strategy(budget, name, start, graph.StartCash, DefaultHorizonMonths, scope.Now(), currentUser.UserId);
        strategy.Replace(name, start, graph.StartCash, DefaultHorizonMonths, graph.Nodes, graph.Edges, [], scope.Now());

        db.Strategies.Add(strategy);
        await db.SaveChangesAsync(ct);

        return StrategyMapping.ToResponse(strategy, StrategySimulator.Run(StrategyMapping.ToInput(strategy)));
    }
}
