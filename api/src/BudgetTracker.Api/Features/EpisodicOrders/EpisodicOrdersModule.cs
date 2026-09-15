using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
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

    /// <summary>Komendy CLI (issue #25) — te same handlery co endpointy REST wyżej.</summary>
    public static CliCommandRegistry MapEpisodicOrdersCli(this CliCommandRegistry registry)
    {
        registry.Register("episodic-order", "list", "Zaplanowane i zrealizowane zlecenia epizodyczne.",
            "episodic-order list [--budget-id <guid>]",
            [CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego.")],
            async (sp, args, ct) => await sp.GetRequiredService<GetEpisodicOrdersQueryHandler>()
                .HandleAsync(args.GetGuidFlag("budget-id"), ct));

        registry.Register("episodic-order", "candidates", "Wydatki, które mogą zrealizować zlecenie.",
            "episodic-order candidates [--budget-id <guid>] [--order-id <guid>] [--search <fraza>]",
            [
                CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego."),
                CliFlag.Optional("order-id", "Zlecenie, dla którego szukamy kandydatów (dialog „Oznacz jako kupione”)."),
                CliFlag.Optional("search", "Fraza w opisie transakcji."),
            ],
            async (sp, args, ct) => await sp.GetRequiredService<GetEpisodicOrderCandidatesQueryHandler>()
                .HandleAsync(args.GetGuidFlag("budget-id"), args.GetGuidFlag("order-id"), args.GetFlag("search"), ct));

        registry.Register("episodic-order", "create", "Tworzy zlecenie epizodyczne — zaplanowane albo od razu zrealizowane.",
            EpisodicOrderUsage("create"), EpisodicOrderFlags(),
            async (sp, args, ct) =>
                await sp.GetRequiredService<CreateEpisodicOrderCommandHandler>().HandleAsync(ToSaveRequest(args), ct));

        registry.Register("episodic-order", "update", "Zmienia zlecenie epizodyczne (budżet i transakcja są ignorowane przy zmianie).",
            EpisodicOrderUsage("update <id>"), EpisodicOrderFlags(),
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<UpdateEpisodicOrderCommandHandler>().HandleAsync(id, ToSaveRequest(args), ct);
                return new { updated = true, id };
            });

        registry.Register("episodic-order", "delete", "Usuwa zlecenie epizodyczne (razem z nierozliczoną rezerwacją, jeśli jest).",
            "episodic-order delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<DeleteEpisodicOrderCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        registry.Register("episodic-order", "add-goal", "„Załóż cel oszczędzania” — tworzy rezerwację rządzoną przez to zlecenie.",
            "episodic-order add-goal <id>", [],
            async (sp, args, ct) =>
                await sp.GetRequiredService<CreateEpisodicOrderReservationCommandHandler>()
                    .HandleAsync(args.GetGuid(0), ct));

        registry.Register("episodic-order", "realize", "„Oznacz jako kupione” — rozlicza plan wskazaną transakcją.",
            "episodic-order realize <id> --transaction-id <guid>",
            [CliFlag.Required("transaction-id", "Transakcja, którą zapłacono zaplanowany zakup.")],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                var request = new PurchaseEpisodicOrderRequestDto(args.GetRequiredGuidFlag("transaction-id"));
                await sp.GetRequiredService<PurchaseEpisodicOrderCommandHandler>().PurchaseAsync(id, request, ct);
                return new { realized = true, id };
            });

        registry.Register("episodic-order", "unrealize", "Cofa realizację do zaplanowanych (tylko jeśli było zaplanowane).",
            "episodic-order unrealize <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<PurchaseEpisodicOrderCommandHandler>().UndoAsync(id, ct);
                return new { unrealized = true, id };
            });

        return registry;
    }

    private static string EpisodicOrderUsage(string verbAndArgs) =>
        $"episodic-order {verbAndArgs} --name <nazwa> [--description <opis>] [--transaction-id <guid>] "
        + "[--category-id <guid>] [--amount <kwota>] [--due-month <RRRR-MM>] [--budget-id <guid>]";

    private static IReadOnlyList<CliFlagDefinition> EpisodicOrderFlags() =>
    [
        CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego. Przy „update” ignorowany."),
        CliFlag.Required("name", "Nazwa zlecenia (np. „Nowy laptop”)."),
        CliFlag.Optional("description", "Opis — po co, jakie parametry."),
        CliFlag.Optional("transaction-id",
            "Podaj, żeby utworzyć OD RAZU zrealizowane (kwotę/datę/kategorię bierze z transakcji) — wtedy plan pomiń."),
        CliFlag.Optional("category-id", "Kategoria planu — pomiń przy --transaction-id."),
        CliFlag.Optional("amount", "Kwota planu — pomiń przy --transaction-id."),
        CliFlag.Optional("due-month", "Termin planu (dowolny dzień miesiąca) — pomiń przy --transaction-id."),
    ];

    private static SaveEpisodicOrderRequestDto ToSaveRequest(CliArgs args) => new(
        args.GetGuidFlag("budget-id"),
        args.GetRequiredFlag("name"),
        args.GetFlag("description"),
        args.GetGuidFlag("transaction-id"),
        args.GetGuidFlag("category-id"),
        args.GetDecimalFlag("amount"),
        args.GetDateFlag("due-month"));
}
