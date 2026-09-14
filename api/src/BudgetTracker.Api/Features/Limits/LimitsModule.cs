using BudgetTracker.Api.Features.Limits.Commands;
using BudgetTracker.Api.Features.Limits.Contracts;
using BudgetTracker.Api.Features.Limits.Queries;
using BudgetTracker.Api.Features.Limits.Services;

namespace BudgetTracker.Api.Features.Limits;

/// <summary>Rejestracja DI i endpointy limitów wydatków — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>. Stąd <c>Produces</c>.</item>
/// <item><c>budgetId</c> jest POJEDYNCZY, w odróżnieniu od transakcji i oszczędności: limity są zasadą jednego
/// budżetu, a suma limitów dwóch wariantów tych samych danych nie znaczy nic.</item>
/// <item>Usunięcie to 204 — limit, który objął zamknięte miesiące, jest kończony, a nie kasowany.</item>
/// </list>
/// </remarks>
public static class LimitsModule
{
    public static IServiceCollection AddLimits(this IServiceCollection services)
    {
        services.AddScoped<LimitsBudgetScope>();
        services.AddScoped<LimitCategories>();

        services.AddScoped<GetLimitsQueryHandler>();
        services.AddScoped<SetLimitCommandHandler>();
        services.AddScoped<RemoveLimitCommandHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapLimits(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/limits", async (
            Guid? budgetId, DateOnly? month, GetLimitsQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, month, ct)))
            .WithName("GetLimits")
            .Produces<LimitsResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/limits", async (
            SetLimitRequestDto request, SetLimitCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("SetLimit")
            .Produces<LimitSavedResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapDelete("/api/limits/{id:guid}", async (
            Guid id, RemoveLimitCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("RemoveLimit")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
