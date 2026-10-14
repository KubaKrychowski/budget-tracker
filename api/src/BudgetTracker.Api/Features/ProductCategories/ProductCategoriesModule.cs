using BudgetTracker.Api.Features.ProductCategories.Commands;
using BudgetTracker.Api.Features.ProductCategories.Contracts;
using BudgetTracker.Api.Features.ProductCategories.Queries;
using BudgetTracker.Api.Features.ProductCategories.Services;

namespace BudgetTracker.Api.Features.ProductCategories;

/// <summary>Rejestracja DI i endpointy kategorii produktów z paragonów — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// Wybór kategorii dla pozycji paragonu to endpoint paragonów (<c>PUT /api/receipts/{id}/items/{index}/category</c>), bo pozycja leży w paragonie.
/// Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>.
/// </remarks>
public static class ProductCategoriesModule
{
    public static IServiceCollection AddProductCategories(this IServiceCollection services)
    {
        services.AddScoped<ProductRuleCategorizer>();
        services.AddScoped<ProductMlCategorizer>();
        services.AddScoped<ProductCategorizer>();
        services.AddScoped<ProductRuleLearning>();

        services.AddScoped<CreateProductCategoryCommandHandler>();
        services.AddScoped<GetProductCategoryListQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapProductCategories(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/product-categories", async (GetProductCategoryListQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("ListProductCategories")
            .Produces<IReadOnlyList<ProductCategoryResponseDto>>();

        app.MapPost("/api/product-categories", async (
            ProductCategoryRequestDto request, CreateProductCategoryCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("CreateProductCategory")
            .Produces<ProductCategoryResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
