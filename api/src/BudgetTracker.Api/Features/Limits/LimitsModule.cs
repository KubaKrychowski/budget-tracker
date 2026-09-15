using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
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

    /// <summary>Komendy CLI (issue #25) — te same handlery co endpointy REST wyżej.</summary>
    public static CliCommandRegistry MapLimitsCli(this CliCommandRegistry registry)
    {
        registry.Register("limit", "list", "Limity kategorii i wykorzystanie w miesiącu.",
            "limit list [--budget-id <guid>] [--month <RRRR-MM-01>]",
            [
                CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego."),
                CliFlag.Optional("month", "Oglądany miesiąc (dzień ignorowany); domyślnie bieżący."),
            ],
            async (sp, args, ct) => await sp.GetRequiredService<GetLimitsQueryHandler>()
                .HandleAsync(args.GetGuidFlag("budget-id"), args.GetDateFlag("month"), ct));

        registry.Register("limit", "set", "Ustawia albo zmienia limit kategorii od wskazanego miesiąca.",
            "limit set --category-id <guid> --amount <kwota> --warning-threshold <1-100> --valid-from <RRRR-MM-01> [--budget-id <guid>]",
            [
                CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego."),
                CliFlag.Required("category-id", "BusinessId kategorii."),
                CliFlag.Required("amount", "Kwota limitu."),
                CliFlag.Required("warning-threshold", "Próg ostrzeżenia w procentach (1–100)."),
                CliFlag.Required("valid-from", "Miesiąc, od którego limit obowiązuje."),
            ],
            async (sp, args, ct) =>
            {
                var request = new SetLimitRequestDto(
                    args.GetGuidFlag("budget-id"),
                    args.GetRequiredGuidFlag("category-id"),
                    args.GetRequiredDecimalFlag("amount"),
                    args.GetRequiredIntFlag("warning-threshold"),
                    args.GetRequiredDateFlag("valid-from"));
                return await sp.GetRequiredService<SetLimitCommandHandler>().HandleAsync(request, ct);
            });

        registry.Register("limit", "delete", "Kończy limit (miniony miesiąc zostaje historycznie policzony).",
            "limit delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<RemoveLimitCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        return registry;
    }
}
