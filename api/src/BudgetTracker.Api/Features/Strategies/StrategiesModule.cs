using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Features.Strategies.Commands;
using BudgetTracker.Api.Features.Strategies.Consts;
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
        services.AddScoped<SimulateStrategyVariantsQueryHandler>();
        services.AddScoped<CreateStrategyCommandHandler>();
        services.AddScoped<SaveStrategyCommandHandler>();
        services.AddScoped<DeleteStrategyCommandHandler>();
        services.AddScoped<DuplicateStrategyCommandHandler>();
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

        app.MapPost("/api/strategies/{id:guid}/duplicate", async (
            Guid id, DuplicateStrategyRequestDto request, DuplicateStrategyCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("DuplicateStrategy")
            .Produces<StrategyResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/strategies/simulate", async (
            SaveStrategyRequestDto request, SimulateStrategyQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("SimulateStrategy")
            .Produces<StrategyResultResponseDto>()
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPost("/api/strategies/simulate-variants", async (
            SaveStrategyRequestDto request, SimulateStrategyVariantsQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("SimulateStrategyVariants")
            .Produces<StrategyVariantsResultResponseDto>()
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
            Guid id, string? variantId, GetStrategyApplyPreviewQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, variantId, ct)))
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

    /// <summary>Komendy CLI (issue #25) — te same handlery co endpointy REST wyżej.</summary>
    public static CliCommandRegistry MapStrategiesCli(this CliCommandRegistry registry)
    {
        const string BudgetIdHelp = "BusinessId budżetu; pomiń dla budżetu domyślnego.";
        const string VariantIdHelp = "Identyfikator wariantu (z „strategy get”); pomiń dla bazowego.";
        const string GraphHelp =
            "Cała strategia jako JSON (SaveStrategyRequestDto): name, startMonth, startCash, horizonMonths, nodes[], edges[], variants[].";

        registry.Register("strategy", "list", "Strategie budżetu (nazwa, liczba zdarzeń i akcji, data zmiany).",
            "strategy list [--budget-id <guid>]",
            [CliFlag.Optional("budget-id", BudgetIdHelp)],
            async (sp, args, ct) => await sp.GetRequiredService<ListStrategiesQueryHandler>()
                .HandleAsync(args.GetGuidFlag("budget-id"), ct));

        registry.Register("strategy", "get", "Strategia z całym grafem i policzonym wynikiem symulacji.",
            "strategy get <id>", [],
            async (sp, args, ct) => await sp.GetRequiredService<GetStrategyQueryHandler>().HandleAsync(args.GetGuid(0), ct));

        registry.Register("strategy", "create", "Zakłada strategię: pustą (Blank) albo z szablonu (LoanAndCushion).",
            "strategy create --name <nazwa> [--template Blank|LoanAndCushion] [--budget-id <guid>]",
            [
                CliFlag.Optional("budget-id", BudgetIdHelp),
                CliFlag.Required("name", "Nazwa strategii (do 100 znaków)."),
                CliFlag.Optional("template", "Szablon startowy: Blank (domyślnie) albo LoanAndCushion."),
            ],
            async (sp, args, ct) => await sp.GetRequiredService<CreateStrategyCommandHandler>().HandleAsync(
                new CreateStrategyRequestDto(
                    args.GetGuidFlag("budget-id"), args.GetRequiredFlag("name"),
                    args.GetEnumFlag("template", StrategyTemplate.Blank)),
                ct));

        registry.Register("strategy", "duplicate", "Kopiuje strategię (graf, parametry i domyślnie warianty) pod nową nazwą.",
            "strategy duplicate <id> --name <nazwa> [--variants true|false]",
            [
                CliFlag.Required("name", "Nazwa kopii (do 100 znaków)."),
                CliFlag.Optional("variants", "Czy kopiować warianty: true (domyślnie) albo false."),
            ],
            async (sp, args, ct) => await sp.GetRequiredService<DuplicateStrategyCommandHandler>().HandleAsync(
                args.GetGuid(0),
                new DuplicateStrategyRequestDto(args.GetRequiredFlag("name"), args.GetBoolFlag("variants", true)),
                ct));

        registry.Register("strategy", "save", "Zapisuje całą strategię (parametry i graf) — jak „Zapisz strategię” na tablicy.",
            "strategy save <id> --json '<strategia>'",
            [CliFlag.Required("json", GraphHelp)],
            async (sp, args, ct) => await sp.GetRequiredService<SaveStrategyCommandHandler>()
                .HandleAsync(args.GetGuid(0), args.GetRequiredJsonFlag<SaveStrategyRequestDto>("json"), ct));

        registry.Register("strategy", "simulate", "Liczy symulację niezapisanego grafu — niczego nie zapisuje.",
            "strategy simulate --json '<strategia>'",
            [CliFlag.Required("json", GraphHelp)],
            async (sp, args, ct) => await sp.GetRequiredService<SimulateStrategyQueryHandler>()
                .HandleAsync(args.GetRequiredJsonFlag<SaveStrategyRequestDto>("json"), ct));

        registry.Register("strategy", "simulate-variants", "Liczy symulację niezapisanego grafu dla wariantu bazowego i wszystkich wariantów — niczego nie zapisuje.",
            "strategy simulate-variants --json '<strategia>'",
            [CliFlag.Required("json", GraphHelp)],
            async (sp, args, ct) => await sp.GetRequiredService<SimulateStrategyVariantsQueryHandler>()
                .HandleAsync(args.GetRequiredJsonFlag<SaveStrategyRequestDto>("json"), ct));

        registry.Register("strategy", "delete", "Usuwa strategię (to, co z niej założono w budżecie, zostaje).",
            "strategy delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<DeleteStrategyCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        registry.Register("strategy", "references", "Kategorie i niezakończone zlecenia stałe budżetu strategii (do pól kafelków).",
            "strategy references <id>", [],
            async (sp, args, ct) => await sp.GetRequiredService<GetStrategyReferencesQueryHandler>()
                .HandleAsync(args.GetGuid(0), ct));

        registry.Register("strategy", "apply-preview", "Co zastosowanie strategii założyłoby w budżecie (statusy akcji).",
            "strategy apply-preview <id> [--variant-id <id>]",
            [CliFlag.Optional("variant-id", VariantIdHelp)],
            async (sp, args, ct) => await sp.GetRequiredService<GetStrategyApplyPreviewQueryHandler>()
                .HandleAsync(args.GetGuid(0), args.GetFlag("variant-id"), ct));

        registry.Register("strategy", "apply", "Zakłada w budżecie wskazane akcje strategii (wszystkie albo żadna).",
            "strategy apply <id> --node-ids <id1,id2,...> [--variant-id <id>]",
            [
                CliFlag.Required("node-ids", "Identyfikatory kafelków (z „strategy apply-preview”) rozdzielone przecinkami."),
                CliFlag.Optional("variant-id", VariantIdHelp),
            ],
            async (sp, args, ct) => await sp.GetRequiredService<ApplyStrategyCommandHandler>().HandleAsync(
                args.GetGuid(0),
                new ApplyStrategyRequestDto(
                    args.GetRequiredFlag("node-ids").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    args.GetFlag("variant-id")),
                ct));

        return registry;
    }
}
