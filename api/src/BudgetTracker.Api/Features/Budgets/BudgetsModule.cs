using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Commands;
using BudgetTracker.Api.Features.Budgets.Consts;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Queries;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure.Contracts;
using BudgetTracker.Api.Resources;
using Hangfire;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Budgets;

/// <summary>Rejestracja DI i endpointy feature'a budżetów — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// Endpointy NIE łapią wyjątków: „nieznany BusinessId = 404" i „pusta nazwa = 400" rozstrzyga
/// <c>DomainExceptionHandler</c>. Kody, które z tego wynikają, deklaruje <c>Produces</c>.
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

        services.AddScoped<CreateBudgetCommandHandler>();
        services.AddScoped<UpdateBudgetCommandHandler>();
        services.AddScoped<SetBudgetEnabledCommandHandler>();
        services.AddScoped<ResetBudgetCommandHandler>();
        services.AddScoped<DeleteBudgetCommandHandler>();
        services.AddScoped<RestoreBudgetCommandHandler>();

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

        return app;
    }

    private static IResult BadRequest(IStringLocalizer<SharedResource> localizer, string resourceKey) =>
        Results.BadRequest(new { error = localizer[resourceKey].Value });
}
