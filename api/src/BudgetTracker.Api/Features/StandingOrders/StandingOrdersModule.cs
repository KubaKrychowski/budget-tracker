using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Features.StandingOrders.Commands;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Queries;
using BudgetTracker.Api.Features.StandingOrders.Services;

namespace BudgetTracker.Api.Features.StandingOrders;

/// <summary>Rejestracja DI i endpointy zleceń stałych — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>. Stąd <c>Produces</c>.</item>
/// <item>Podgląd reguł to POST, bo niesie reguły w ciele, ale NICZEGO nie zapisuje.</item>
/// <item>Zakończenie to zasób <c>/end</c>: PUT ustawia ostatni miesiąc, DELETE go zdejmuje („Wznów”).</item>
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
        services.AddScoped<EndStandingOrderCommandHandler>();
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

        app.MapPut("/api/standing-orders/{id:guid}/end", async (
            Guid id, EndStandingOrderRequestDto request, EndStandingOrderCommandHandler handler, CancellationToken ct) =>
        {
            await handler.EndAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithName("EndStandingOrder")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/standing-orders/{id:guid}/end", async (
            Guid id, EndStandingOrderCommandHandler handler, CancellationToken ct) =>
        {
            await handler.ResumeAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("ResumeStandingOrder")
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

    /// <summary>Komendy CLI (issue #25) — te same handlery co endpointy REST wyżej.</summary>
    public static CliCommandRegistry MapStandingOrdersCli(this CliCommandRegistry registry)
    {
        registry.Register("standing-order", "list", "Zlecenia stałe, ich stan w miesiącu i ostatnio przypięte.",
            "standing-order list [--budget-id <guid>] [--month <RRRR-MM-01>]",
            [
                CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego."),
                CliFlag.Optional("month", "Oglądany miesiąc (dzień ignorowany); domyślnie bieżący."),
            ],
            async (sp, args, ct) => await sp.GetRequiredService<GetStandingOrdersQueryHandler>()
                .HandleAsync(args.GetGuidFlag("budget-id"), args.GetDateFlag("month"), ct));

        registry.Register("standing-order", "preview",
            "Ile transakcji z historii budżetu już pasuje do reguł — bez zapisu.",
            "standing-order preview --rules-json <json> [--budget-id <guid>] [--standing-order-id <guid>]",
            [
                CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego."),
                CliFlag.Optional("standing-order-id",
                    "Zlecenie w edycji — jego ręczne odpięcia nie liczą się do podglądu."),
                CliFlag.Required("rules-json",
                    "Tablica JSON: [{\"titlePattern\":\"...\",\"amountFrom\":10,\"amountTo\":50}]."),
            ],
            async (sp, args, ct) =>
            {
                var request = new StandingOrderPreviewRequestDto(
                    args.GetGuidFlag("budget-id"),
                    args.GetGuidFlag("standing-order-id"),
                    args.GetRequiredJsonFlag<IReadOnlyList<StandingOrderRuleRequestDto>>("rules-json"));
                return await sp.GetRequiredService<PreviewStandingOrderQueryHandler>().HandleAsync(request, ct);
            });

        registry.Register("standing-order", "create", "Tworzy zlecenie stałe.", StandingOrderUsage("create"),
            StandingOrderFlags(),
            async (sp, args, ct) =>
                await sp.GetRequiredService<CreateStandingOrderCommandHandler>().HandleAsync(ToSaveRequest(args), ct));

        registry.Register("standing-order", "update", "Zmienia zlecenie stałe (budżet nie przechodzi między zleceniami).",
            StandingOrderUsage("update <id>"), StandingOrderFlags(),
            async (sp, args, ct) => await sp.GetRequiredService<UpdateStandingOrderCommandHandler>()
                .HandleAsync(args.GetGuid(0), ToSaveRequest(args), ct));

        registry.Register("standing-order", "delete", "Usuwa zlecenie stałe.", "standing-order delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<DeleteStandingOrderCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        registry.Register("standing-order", "end", "Kończy zlecenie od wskazanego miesiąca (dalej nieoczekiwane, nieprzypinane).",
            "standing-order end <id> --last-month <RRRR-MM-01>",
            [CliFlag.Required("last-month", "Dowolny dzień ostatniego miesiąca zlecenia; liczy się rok i miesiąc.")],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<EndStandingOrderCommandHandler>()
                    .EndAsync(id, new EndStandingOrderRequestDto(args.GetRequiredDateFlag("last-month")), ct);
                return new { ended = true, id };
            });

        registry.Register("standing-order", "reopen", "Cofa zakończenie zlecenia stałego („Wznów”).",
            "standing-order reopen <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<EndStandingOrderCommandHandler>().ResumeAsync(id, ct);
                return new { resumed = true, id };
            });

        registry.Register("standing-order", "unpin", "Ręcznie odpina transakcję od zlecenia stałego.",
            "standing-order unpin <transactionId>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<UnpinTransactionCommandHandler>().HandleAsync(id, ct);
                return new { unpinned = true, transactionId = id };
            });

        return registry;
    }

    private static string StandingOrderUsage(string verbAndArgs) =>
        $"standing-order {verbAndArgs} --name <nazwa> --expected-amount <kwota> --rhythm Monthly|Quarterly|Yearly "
        + "--rules-json <json> [--due-month <1-12>] [--budget-id <guid>]";

    private static IReadOnlyList<CliFlagDefinition> StandingOrderFlags() =>
    [
        CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego. Przy „update” ignorowany."),
        CliFlag.Required("name", "Nazwa zlecenia (np. „Czynsz”)."),
        CliFlag.Required("expected-amount", "Zwykła kwota zlecenia."),
        CliFlag.Required("rhythm", "Monthly / Quarterly / Yearly."),
        CliFlag.Optional("due-month", "Miesiąc 1–12 dla rytmu kwartalnego/rocznego; przy miesięcznym pomiń."),
        CliFlag.Required("rules-json",
            "Tablica JSON, co najmniej jedna reguła: [{\"titlePattern\":\"...\",\"amountFrom\":10,\"amountTo\":50}]."),
    ];

    private static SaveStandingOrderRequestDto ToSaveRequest(CliArgs args) => new(
        args.GetGuidFlag("budget-id"),
        args.GetRequiredFlag("name"),
        args.GetRequiredDecimalFlag("expected-amount"),
        args.GetEnumFlag("rhythm", StandingOrderRhythm.Monthly),
        args.GetIntFlag("due-month"),
        args.GetRequiredJsonFlag<IReadOnlyList<StandingOrderRuleRequestDto>>("rules-json"));
}
