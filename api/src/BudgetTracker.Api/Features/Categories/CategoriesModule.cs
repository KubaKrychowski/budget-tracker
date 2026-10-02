using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categories.Commands;
using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Features.Categories.Queries;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;

namespace BudgetTracker.Api.Features.Categories;

/// <summary>Rejestracja DI i endpointy własnych kategorii — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// Słownik do selectów (<c>GET /api/categories</c>) zostaje w module Transactions; tu jest zarządzanie.
/// Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>.
/// </remarks>
public static class CategoriesModule
{
    public static IServiceCollection AddCategories(this IServiceCollection services)
    {
        services.AddScoped<CategoryNames>();
        services.AddScoped<CategoryUsage>();
        services.AddScoped<CategoryLookup>();

        services.AddScoped<CreateCategoryCommandHandler>();
        services.AddScoped<UpdateCategoryCommandHandler>();
        services.AddScoped<DeleteCategoryCommandHandler>();
        services.AddScoped<GetCategoryListQueryHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapCategories(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories/manage", async (GetCategoryListQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("ListManagedCategories")
            .Produces<IReadOnlyList<CategoryResponseDto>>();

        app.MapPost("/api/categories", async (
            CategoryRequestDto request, CreateCategoryCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("CreateCategory")
            .Produces<CategoryResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        app.MapPut("/api/categories/{id:guid}", async (
            Guid id, CategoryRequestDto request, UpdateCategoryCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("UpdateCategory")
            .Produces<CategoryResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapDelete("/api/categories/{id:guid}", async (
            Guid id, DeleteCategoryCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("DeleteCategory")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>Komendy CLI: <c>category manage/create/update/delete</c> (lista słownikowa jest w module Transactions).</summary>
    public static CliCommandRegistry MapCategoriesCli(this CliCommandRegistry registry)
    {
        registry.Register("category", "manage", "Kategorie z użyciem: własne i wspólne.", "category manage", [],
            async (sp, _, ct) => await sp.GetRequiredService<GetCategoryListQueryHandler>().HandleAsync(ct));

        registry.Register("category", "create", "Tworzy własną kategorię.", Usage("create"), Flags(),
            async (sp, args, ct) =>
                await sp.GetRequiredService<CreateCategoryCommandHandler>().HandleAsync(ToRequest(args), ct));

        registry.Register("category", "update", "Zmienia nazwę i typ własnej kategorii.", Usage("update <id>"), Flags(),
            async (sp, args, ct) => await sp.GetRequiredService<UpdateCategoryCommandHandler>()
                .HandleAsync(args.GetGuid(0), ToRequest(args), ct));

        registry.Register("category", "delete", "Usuwa własną, nieużywaną kategorię.", "category delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<DeleteCategoryCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        return registry;
    }

    private static string Usage(string verbAndArgs) => $"category {verbAndArgs} --name <nazwa> --type Expense|Income";

    private static IReadOnlyList<CliFlagDefinition> Flags() =>
    [
        CliFlag.Required("name", "Nazwa kategorii, unikalna wśród własnych i wspólnych."),
        CliFlag.Required("type", "Expense (wydatek) albo Income (wpływ)."),
    ];

    private static CategoryRequestDto ToRequest(CliArgs args) =>
        new(args.GetFlag("name"), args.GetEnumFlag("type", CategoryType.Expense));
}
