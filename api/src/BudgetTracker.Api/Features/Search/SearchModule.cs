using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Features.Search.Contracts;
using BudgetTracker.Api.Features.Search.Queries;

namespace BudgetTracker.Api.Features.Search;

/// <summary>Rejestracja DI i endpoint wyszukiwarki z nagłówka — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// Jeden endpoint zamiast pięciu per rodzaj: menu i tak potrzebuje kompletu, zanim cokolwiek pokaże,
/// a pięć równoległych żądań na każde naciśnięcie klawisza to pięć razy więcej okazji do wyścigu
/// między odpowiedziami do różnych fraz.
/// </remarks>
public static class SearchModule
{
    public static IServiceCollection AddSearch(this IServiceCollection services)
    {
        services.AddScoped<GetSearchResultsQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", async (
            string? q, Guid? budgetId, bool? allBudgets, GetSearchResultsQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(q, budgetId, allBudgets ?? false, ct)))
            .WithName("Search")
            .Produces<SearchResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Komenda CLI (issue #25) — ten sam handler co endpoint wyżej.</summary>
    public static CliCommandRegistry MapSearchCli(this CliCommandRegistry registry)
    {
        registry.Register("search", "run", "Szuka po transakcjach, kategoriach, budżetach i zleceniach.",
            "search run --query <fraza> [--budget-id <guid>] [--all-budgets]",
            [
                CliFlag.Required("query", "Szukana fraza (minimum 2 znaki)."),
                CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego."),
                CliFlag.Optional("all-budgets", "Szuka we wszystkich budżetach zamiast w jednym."),
            ],
            async (sp, args, ct) => await sp.GetRequiredService<GetSearchResultsQueryHandler>()
                .HandleAsync(
                    args.GetRequiredFlag("query"),
                    args.GetGuidFlag("budget-id"),
                    args.GetBoolFlag("all-budgets"),
                    ct));

        return registry;
    }
}
