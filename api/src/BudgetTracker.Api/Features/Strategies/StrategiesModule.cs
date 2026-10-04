using BudgetTracker.Api.Features.Strategies.Commands;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Queries;
using BudgetTracker.Api.Features.Strategies.Services;

namespace BudgetTracker.Api.Features.Strategies;

/// <summary>Rejestracja DI i endpointy strategii — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>. Stąd <c>Produces</c>.</item>
/// <item>Symulacja niezapisanego grafu to POST, bo niesie graf w ciele, ale NICZEGO nie zapisuje.</item>
/// <item>Zapis to PUT całej strategii — klient trzyma stan tablicy i wysyła go w całości.</item>
/// </list>
/// </remarks>
public static class StrategiesModule
{
    public static IServiceCollection AddStrategies(this IServiceCollection services)
    {
        services.AddScoped<StrategiesBudgetScope>();
        services.AddScoped<StrategyApplyPlanner>();

        services.AddScoped<ListStrategiesQueryHandler>();
        services.AddScoped<GetStrategyQueryHandler>();
        services.AddScoped<SimulateStrategyQueryHandler>();
        services.AddScoped<CreateStrategyCommandHandler>();
        services.AddScoped<SaveStrategyCommandHandler>();
        services.AddScoped<DeleteStrategyCommandHandler>();
        services.AddScoped<GetStrategyApplyPreviewQueryHandler>();
        services.AddScoped<ApplyStrategyCommandHandler>();
        services.AddScoped<GetStrategyReferencesQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapStrategies(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/strategies", async (Guid? budgetId, ListStrategiesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, ct)))
            .WithName("ListStrategies")
            .Produces<StrategiesResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/strategies", async (
            CreateStrategyRequestDto request, CreateStrategyCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("CreateStrategy")
            .Produces<StrategyResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/strategies/simulate", async (
            SaveStrategyRequestDto request, SimulateStrategyQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("SimulateStrategy")
            .Produces<StrategyResultResponseDto>()
            .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/api/strategies/{id:guid}", async (Guid id, GetStrategyQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetStrategy")
            .Produces<StrategyResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/strategies/{id:guid}", async (
            Guid id, SaveStrategyRequestDto request, SaveStrategyCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("SaveStrategy")
            .Produces<StrategyResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/strategies/{id:guid}/apply", async (
            Guid id, GetStrategyApplyPreviewQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetStrategyApplyPreview")
            .Produces<StrategyApplyPreviewResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/strategies/{id:guid}/references", async (
            Guid id, GetStrategyReferencesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetStrategyReferences")
            .Produces<StrategyReferencesResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/strategies/{id:guid}/apply", async (
            Guid id, ApplyStrategyRequestDto request, ApplyStrategyCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("ApplyStrategy")
            .Produces<ApplyStrategyResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapDelete("/api/strategies/{id:guid}", async (Guid id, DeleteStrategyCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("DeleteStrategy")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
