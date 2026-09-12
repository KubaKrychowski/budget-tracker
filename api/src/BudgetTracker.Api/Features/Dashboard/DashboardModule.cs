using BudgetTracker.Api.Features.Dashboard.Contracts;
using BudgetTracker.Api.Features.Dashboard.Queries;
using BudgetTracker.Api.Features.Dashboard.Services;
using BudgetTracker.Api.Resources;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Api.Features.Dashboard;

/// <summary>Rejestracja DI i endpoint dashboardu — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Bez parametrów okres to <see cref="DashboardPeriod.Default"/> — dashboard musi coś pokazać bez parametrów.
/// „Dzisiaj" idzie przez <see cref="TimeProvider"/>, nie <c>DateTime.UtcNow</c>: inaczej nie da się go podmienić
/// w teście i zachowanie zależy od zegara maszyny.</item>
/// <item>Nieznany budżet = 404, nie cichy fallback na domyślny (inaczej zły adres pokazywałby liczby innego budżetu
/// jako swoje). Mapuje to <c>DomainExceptionHandler</c>, więc endpoint nie ma try/catch — reguła jest przekrojowa
/// i ma jedno miejsce.</item>
/// </list>
/// </remarks>
public static class DashboardModule
{
    public static IServiceCollection AddDashboard(this IServiceCollection services)
    {
        services.AddScoped<GetDashboardQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (
            DateOnly? from, DateOnly? to, Guid? budgetId,
            GetDashboardQueryHandler handler, TimeProvider clock,
            IStringLocalizer<SharedResource> localizer, CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);
            var (defaultFrom, defaultTo) = DashboardPeriod.Default(today);
            var effectiveFrom = from ?? defaultFrom;
            var effectiveTo = to ?? defaultTo;

            if (effectiveTo < effectiveFrom)
                return Results.BadRequest(new { error = localizer["Error_PeriodToBeforeFrom"].Value });

            return Results.Ok(await handler.HandleAsync(effectiveFrom, effectiveTo, budgetId, ct));
        })
        .WithName("GetDashboard")
        .Produces<DashboardResponseDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
