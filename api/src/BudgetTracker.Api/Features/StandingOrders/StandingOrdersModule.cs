using BudgetTracker.Api.Features.StandingOrders.Commands;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Queries;
using BudgetTracker.Api.Features.StandingOrders.Services;

namespace BudgetTracker.Api.Features.StandingOrders;

/// <summary>Rejestracja DI i endpointy zleceń stałych — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>. Stąd <c>Produces</c>.</item>
/// <item>Podgląd reguły to POST, bo niesie regułę w ciele, ale NICZEGO nie zapisuje.</item>
/// <item>„Odepnij” adresuje TRANSAKCJĘ, nie zlecenie — transakcja należy do jednego zlecenia naraz.</item>
/// </list>
/// </remarks>
public static class StandingOrdersModule
{
    public static IServiceCollection AddStandingOrders(this IServiceCollection services)
    {
        services.AddScoped<StandingOrdersBudgetScope>();
        services.AddScoped<StandingOrderMatcher>();

        services.AddScoped<GetStandingOrdersQueryHandler>();
        services.AddScoped<PreviewStandingOrderQueryHandler>();
        services.AddScoped<CreateStandingOrderCommandHandler>();
        services.AddScoped<UpdateStandingOrderCommandHandler>();
        services.AddScoped<DeleteStandingOrderCommandHandler>();
        services.AddScoped<UnpinTransactionCommandHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapStandingOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/standing-orders", async (
            Guid? budgetId, DateOnly? month, GetStandingOrdersQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, month, ct)))
            .WithName("GetStandingOrders")
            .Produces<StandingOrdersResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/standing-orders/preview", async (
            StandingOrderPreviewRequestDto request, PreviewStandingOrderQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("PreviewStandingOrder")
            .Produces<StandingOrderPreviewResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/standing-orders", async (
            SaveStandingOrderRequestDto request, CreateStandingOrderCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("CreateStandingOrder")
            .Produces<StandingOrderSavedResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/standing-orders/{id:guid}", async (
            Guid id, SaveStandingOrderRequestDto request, UpdateStandingOrderCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("UpdateStandingOrder")
            .Produces<StandingOrderSavedResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/standing-orders/{id:guid}", async (
            Guid id, DeleteStandingOrderCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("DeleteStandingOrder")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/standing-orders/pins/{transactionId:guid}", async (
            Guid transactionId, UnpinTransactionCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(transactionId, ct);
            return Results.NoContent();
        })
            .WithName("UnpinStandingOrderTransaction")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
