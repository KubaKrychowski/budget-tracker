using BudgetTracker.Api.Features.EpisodicOrders.Commands;
using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Queries;
using BudgetTracker.Api.Features.EpisodicOrders.Services;

namespace BudgetTracker.Api.Features.EpisodicOrders;

/// <summary>Rejestracja DI i endpointy zleceń epizodycznych — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>. Stąd <c>Produces</c>.</item>
/// <item>„Załóż cel oszczędzania” to zasób <c>/reservation</c>, a „kupione” — <c>/purchase</c>: PUT oznacza,
/// DELETE cofa do zaplanowanych.</item>
/// <item>Kandydaci to osobny odczyt, bo lista zależy od szukajki dialogu, a nie od ekranu.</item>
/// </list>
/// </remarks>
public static class EpisodicOrdersModule
{
    public static IServiceCollection AddEpisodicOrders(this IServiceCollection services)
    {
        services.AddScoped<EpisodicOrdersBudgetScope>();
        services.AddScoped<EpisodicOrderLookup>();
        services.AddScoped<EpisodicOrderRequestValidator>();
        services.AddScoped<EpisodicOrderTransactions>();

        services.AddScoped<GetEpisodicOrdersQueryHandler>();
        services.AddScoped<GetEpisodicOrderCandidatesQueryHandler>();
        services.AddScoped<CreateEpisodicOrderCommandHandler>();
        services.AddScoped<UpdateEpisodicOrderCommandHandler>();
        services.AddScoped<DeleteEpisodicOrderCommandHandler>();
        services.AddScoped<CreateEpisodicOrderReservationCommandHandler>();
        services.AddScoped<PurchaseEpisodicOrderCommandHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapEpisodicOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/episodic-orders", async (
            Guid? budgetId, GetEpisodicOrdersQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, ct)))
            .WithName("GetEpisodicOrders")
            .Produces<EpisodicOrdersResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/episodic-orders/candidates", async (
            Guid? budgetId, Guid? orderId, string? search, GetEpisodicOrderCandidatesQueryHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, orderId, search, ct)))
            .WithName("GetEpisodicOrderCandidates")
            .Produces<IReadOnlyList<EpisodicOrderCandidateResponseDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/episodic-orders", async (
            SaveEpisodicOrderRequestDto request, CreateEpisodicOrderCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("CreateEpisodicOrder")
            .Produces<EpisodicOrderSavedResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/episodic-orders/{id:guid}", async (
            Guid id, SaveEpisodicOrderRequestDto request, UpdateEpisodicOrderCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithName("UpdateEpisodicOrder")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/episodic-orders/{id:guid}", async (
            Guid id, DeleteEpisodicOrderCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("DeleteEpisodicOrder")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/episodic-orders/{id:guid}/reservation", async (
            Guid id, CreateEpisodicOrderReservationCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)))
            .WithName("CreateEpisodicOrderReservation")
            .Produces<EpisodicOrderSavedResponseDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapPut("/api/episodic-orders/{id:guid}/purchase", async (
            Guid id, PurchaseEpisodicOrderRequestDto request, PurchaseEpisodicOrderCommandHandler handler,
            CancellationToken ct) =>
        {
            await handler.PurchaseAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithName("PurchaseEpisodicOrder")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapDelete("/api/episodic-orders/{id:guid}/purchase", async (
            Guid id, PurchaseEpisodicOrderCommandHandler handler, CancellationToken ct) =>
        {
            await handler.UndoAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("UndoEpisodicOrderPurchase")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
