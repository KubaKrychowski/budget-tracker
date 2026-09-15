using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Commands;
using BudgetTracker.Api.Features.Budgets.Consts;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Queries;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Infrastructure.Contracts;
using BudgetTracker.Api.Resources;
using Hangfire;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Budgets;

/// <summary>Rejestracja DI i endpointy feature'a budżetów — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <para>
/// Endpointy NIE łapią wyjątków: „nieznany BusinessId = 404" i „pusta nazwa = 400" rozstrzyga
/// <c>DomainExceptionHandler</c>. Kody, które z tego wynikają, deklaruje <c>Produces</c>.
/// </para>
/// <para>
/// „Odepnij" transfer adresuje TRANSAKCJĘ, nie budżet — transakcja jest przypięta do jednego
/// powiązania naraz (wzorem zleceń stałych).
/// </para>
/// </remarks>
public static class BudgetsModule
{
    public static IServiceCollection AddBudgets(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<BudgetOptions>(config.GetSection(BudgetOptions.SectionName));

        services.AddScoped<BudgetLookup>();
        services.AddScoped<BudgetChildren>();
        services.AddScoped<BudgetListItemReader>();
        services.AddScoped<BudgetPurger>();
        services.AddScoped<SavingsTransferMatcher>();

        services.AddScoped<CreateBudgetCommandHandler>();
        services.AddScoped<UpdateBudgetCommandHandler>();
        services.AddScoped<SetBudgetEnabledCommandHandler>();
        services.AddScoped<ResetBudgetCommandHandler>();
        services.AddScoped<DeleteBudgetCommandHandler>();
        services.AddScoped<RestoreBudgetCommandHandler>();
        services.AddScoped<UpdateSavingsLinkCommandHandler>();
        services.AddScoped<UnpinSavingsTransferCommandHandler>();

        services.AddScoped<GetBudgetsListQueryHandler>();
        services.AddScoped<GetAvailableCurrenciesQueryHandler>();

        services.AddScoped<PurgeDeletedBudgetsCommandHandler>();
        services.AddScoped<ForcePurgeDeletedBudgetsCommandHandler>();
        return services;
    }

    /// <summary>
    /// Zadania feature'a budżetów: sprzątanie usuniętych po oknie retencji (harmonogram z
    /// <see cref="BudgetOptions.PurgeCron"/>) i jego wymuszona wersja, bez harmonogramu.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>purge-deleted-budgets-now</c> stoi na <c>Cron.Never()</c> ŚWIADOMIE. Rejestrujemy je jako
    /// zadanie cykliczne nie po to, żeby kiedykolwiek samo ruszyło, tylko żeby było widoczne w panelu
    /// <c>/hangfire</c> i dało się je odpalić przyciskiem „Trigger now" — Hangfire nie ma innego sposobu
    /// pokazania zadania uruchamianego ręcznie. Powód, dla którego nie wolno dać mu crona, opisuje
    /// <see cref="ForcePurgeDeletedBudgetsCommandHandler"/>.
    ///
    /// Konsekwencja: panel jest dostępny tylko w Development (CLAUDE.md §4), więc na produkcji nie ma
    /// czym tego wyzwolić. Dla narzędzia, które kasuje dane z pominięciem obietnicy retencji, to jest
    /// właściwość, nie brak — pojawi się razem z ekranem, który poprosi człowieka o potwierdzenie.
    /// </remarks>
    public static WebApplication UseBudgetJobs(this WebApplication app)
    {
        var cron = app.Services.GetRequiredService<IOptions<BudgetOptions>>().Value.PurgeCron;
        var jobs = app.Services.GetRequiredService<IRecurringJobManager>();

        jobs.AddOrUpdate<PurgeDeletedBudgetsCommandHandler>(
            "purge-deleted-budgets",
            handler => handler.HandleAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions());

        jobs.AddOrUpdate<ForcePurgeDeletedBudgetsCommandHandler>(
            "purge-deleted-budgets-now",
            handler => handler.HandleAsync(CancellationToken.None),
            Cron.Never(),
            new RecurringJobOptions());

        return app;
    }

    public static IEndpointRouteBuilder MapBudgets(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/currencies", async (
                GetAvailableCurrenciesQueryHandler handler,
                CancellationToken ct) => Results.Ok(await handler.HandleAsync(ct)))
            .WithName("BudgetCurrencies")
            .Produces<IReadOnlyList<DictionaryResponseDto>>();

        app.MapGet("/api/budgets", async (GetBudgetsListQueryHandler handler, CancellationToken ct) =>
                new BudgetListResponseDto(await handler.HandleAsync(ct), handler.RetentionDays))
            .WithName("ListBudgets");

        app.MapPost("/api/budgets", async (
                CreateBudgetRequestDto request,
                CreateBudgetCommandHandler handler,
                IStringLocalizer<SharedResource> localizer,
                CancellationToken ct) =>
            {
                var (budget, error) = await handler.HandleAsync(request, ct);

                return error switch
                {
                    CreateBudgetError.None => Results.Ok(budget),
                    CreateBudgetError.NameRequired => BadRequest(localizer, "Budget_NameRequired"),
                    CreateBudgetError.UnsupportedCurrency => BadRequest(localizer, "Budget_UnsupportedCurrency"),
                    _ => BadRequest(localizer, "Budget_NameRequired"),
                };
            })
            .WithName("CreateBudget");

        app.MapPut("/api/budgets/{businessId:guid}", async (
                Guid businessId,
                UpdateBudgetRequestDto request,
                UpdateBudgetCommandHandler handler,
                CancellationToken ct) => await handler.HandleAsync(businessId, request, ct))
            .WithName("UpdateBudget")
            .Produces<BudgetListItemResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/budgets/{businessId:guid}/disable", async (
                    Guid businessId, SetBudgetEnabledCommandHandler handler, CancellationToken ct) =>
                await handler.HandleAsync(businessId, enabled: false, ct))
            .WithName("DisableBudget")
            .Produces<BudgetListItemResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/budgets/{businessId:guid}/enable", async (
                    Guid businessId, SetBudgetEnabledCommandHandler handler, CancellationToken ct) =>
                await handler.HandleAsync(businessId, enabled: true, ct))
            .WithName("EnableBudget")
            .Produces<BudgetListItemResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/budgets/{businessId:guid}/reset", async (
                    Guid businessId, ResetBudgetCommandHandler handler, CancellationToken ct) =>
                await handler.HandleAsync(businessId, ct))
            .WithName("ResetBudget")
            .Produces<BudgetListItemResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/budgets/{businessId:guid}/restore", async (
                    Guid businessId, RestoreBudgetCommandHandler handler, CancellationToken ct) =>
                await handler.HandleAsync(businessId, ct))
            .WithName("RestoreBudget")
            .Produces<BudgetListItemResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/budgets/{businessId:guid}", async (
                Guid businessId, DeleteBudgetCommandHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(businessId, ct);
                return Results.NoContent();
            })
            .WithName("DeleteBudget")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/budgets/{businessId:guid}/savings-link", async (
                Guid businessId,
                UpdateSavingsLinkRequestDto request,
                UpdateSavingsLinkCommandHandler handler,
                CancellationToken ct) => Results.Ok(await handler.HandleAsync(businessId, request, ct)))
            .WithName("UpdateBudgetSavingsLink")
            .Produces<SavingsLinkSavedResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/budgets/savings-transfer/pins/{transactionId:guid}", async (
                Guid transactionId, UnpinSavingsTransferCommandHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(transactionId, ct);
                return Results.NoContent();
            })
            .WithName("UnpinSavingsTransfer")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// Komendy CLI (issue #25) — każda woła TEN SAM handler co odpowiedni endpoint REST wyżej. Błędy
    /// walidacji zwracane jako wynik (np. <see cref="CreateBudgetResponseDto.Error"/>) wracają wprost —
    /// bez tłumaczenia na komunikat: CLI mówi kodem błędu, nie zdaniem dla ekranu. Wyjątki domenowe
    /// (np. <see cref="Exceptions.BudgetNameRequiredException"/>) lecą dalej do <c>DomainExceptionHandler</c>.
    /// </summary>
    public static CliCommandRegistry MapBudgetsCli(this CliCommandRegistry registry)
    {
        registry.Register("budget", "list", "Lista budżetów, także usuniętych.", "budget list", [],
            async (sp, _, ct) =>
            {
                var handler = sp.GetRequiredService<GetBudgetsListQueryHandler>();
                return new BudgetListResponseDto(await handler.HandleAsync(ct), handler.RetentionDays);
            });

        registry.Register("budget", "currencies", "Dostępne kody walut.", "budget currencies", [],
            async (sp, _, ct) =>
                await sp.GetRequiredService<GetAvailableCurrenciesQueryHandler>().HandleAsync(ct));

        registry.Register("budget", "create", "Tworzy budżet (miesiąc = bieżący).",
            "budget create --name <nazwa> --currency <PLN> --initial-balance <kwota> [--linked-savings-name <nazwa>]",
            [
                CliFlag.Required("name", "Nazwa budżetu."),
                CliFlag.Required("currency", "Kod waluty (patrz „budget currencies”)."),
                CliFlag.Required("initial-balance", "Saldo początkowe."),
                CliFlag.Optional("linked-savings-name",
                    "Niepuste = od razu tworzy i łączy drugi budżet o tej nazwie jako oszczędnościowy."),
            ],
            async (sp, args, ct) =>
            {
                var request = new CreateBudgetRequestDto(
                    args.GetRequiredFlag("name"),
                    args.GetRequiredFlag("currency"),
                    args.GetRequiredDecimalFlag("initial-balance"),
                    args.GetFlag("linked-savings-name"));
                return await sp.GetRequiredService<CreateBudgetCommandHandler>().HandleAsync(request, ct);
            });

        registry.Register("budget", "update", "Zmienia nazwę i saldo początkowe.",
            "budget update <id> --name <nazwa> --initial-balance <kwota>",
            [CliFlag.Required("name", "Nowa nazwa."), CliFlag.Required("initial-balance", "Nowe saldo początkowe.")],
            async (sp, args, ct) =>
            {
                var request = new UpdateBudgetRequestDto(
                    args.GetRequiredFlag("name"), args.GetRequiredDecimalFlag("initial-balance"));
                return await sp.GetRequiredService<UpdateBudgetCommandHandler>()
                    .HandleAsync(args.GetGuid(0), request, ct);
            });

        registry.Register("budget", "disable", "Zamyka budżet na nowy import i nowe transakcje.",
            "budget disable <id>", [],
            async (sp, args, ct) => await sp.GetRequiredService<SetBudgetEnabledCommandHandler>()
                .HandleAsync(args.GetGuid(0), enabled: false, ct));

        registry.Register("budget", "enable", "Otwiera z powrotem wyłączony budżet.",
            "budget enable <id>", [],
            async (sp, args, ct) => await sp.GetRequiredService<SetBudgetEnabledCommandHandler>()
                .HandleAsync(args.GetGuid(0), enabled: true, ct));

        registry.Register("budget", "reset", "Usuwa transakcje, importy i limity — zostaje nazwa, saldo, cel i rezerwacje.",
            "budget reset <id>", [],
            async (sp, args, ct) =>
                await sp.GetRequiredService<ResetBudgetCommandHandler>().HandleAsync(args.GetGuid(0), ct));

        registry.Register("budget", "restore", "Cofa usunięcie budżetu i jego danych (w oknie retencji).",
            "budget restore <id>", [],
            async (sp, args, ct) =>
                await sp.GetRequiredService<RestoreBudgetCommandHandler>().HandleAsync(args.GetGuid(0), ct));

        registry.Register("budget", "delete", "Usuwa budżet: to co reset, plus cele i rezerwacje.",
            "budget delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<DeleteBudgetCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        registry.Register("budget", "link-savings",
            "Zmienia powiązanie z budżetem oszczędnościowym i jego reguły transferu.",
            "budget link-savings <id> [--linked-budget-id <guid>] [--rules-json <json>]",
            [
                CliFlag.Optional("linked-budget-id", "BusinessId budżetu oszczędnościowego; pomiń, żeby zdjąć powiązanie."),
                CliFlag.Optional("rules-json",
                    "Tablica reguł JSON: [{\"titlePattern\":\"...\",\"amountFrom\":10,\"amountTo\":50}]. Wymagane, gdy podajesz --linked-budget-id."),
            ],
            async (sp, args, ct) =>
            {
                var linkedId = args.GetGuidFlag("linked-budget-id");
                var rules = linkedId is not null
                    ? args.GetRequiredJsonFlag<IReadOnlyList<TitleAmountRuleRequestDto>>("rules-json")
                    : [];
                var request = new UpdateSavingsLinkRequestDto(linkedId, rules);
                return await sp.GetRequiredService<UpdateSavingsLinkCommandHandler>()
                    .HandleAsync(args.GetGuid(0), request, ct);
            });

        registry.Register("budget", "unpin-transfer",
            "Ręcznie odpina transakcję od transferu na budżet oszczędnościowy.",
            "budget unpin-transfer <transactionId>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<UnpinSavingsTransferCommandHandler>().HandleAsync(id, ct);
                return new { unpinned = true, transactionId = id };
            });

        return registry;
    }

    private static IResult BadRequest(IStringLocalizer<SharedResource> localizer, string resourceKey) =>
        Results.BadRequest(new { error = localizer[resourceKey].Value });
}
