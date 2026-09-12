using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Transactions.Commands;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Queries;
using BudgetTracker.Api.Features.Transactions.Services;

namespace BudgetTracker.Api.Features.Transactions;

/// <summary>Rejestracja DI i endpointy listy transakcji — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Endpointy NIE łapią wyjątków — „nieznany budżet/transakcja/kategoria = 404" rozstrzyga
/// <c>DomainExceptionHandler</c> w jednym miejscu (ten sam wzorzec co w budżetach).</item>
/// <item><c>budgetId</c> listy jest TABLICĄ, ale nazwa parametru została w liczbie pojedynczej celowo:
/// adresy z jednym budżetem (odnośniki z dashboardu, zakładki użytkownika) działają dalej bez zmian,
/// a wiele budżetów zapisuje się powtórzeniem <c>?budgetId=…&amp;budgetId=…</c>.</item>
/// <item>Edycja inline — pojedyncza i masowa — idzie jednym endpointem i jednym mechanizmem:
/// wszystko w jednej transakcji, wszystko albo nic.</item>
/// </list>
/// </remarks>
public static class TransactionsModule
{
    public static IServiceCollection AddTransactions(this IServiceCollection services)
    {
        services.AddScoped<TransactionBudgetScope>();
        services.AddScoped<TransactionFilters>();
        services.AddScoped<TransactionSelectionResolver>();
        services.AddScoped<TransactionListItemReader>();

        services.AddScoped<UpdateTransactionsCommandHandler>();
        services.AddScoped<BulkDeleteTransactionsCommandHandler>();
        services.AddScoped<BulkSetTransactionCategoryCommandHandler>();
        services.AddScoped<BulkSetTransactionLargeExpenseCommandHandler>();

        services.AddScoped<GetTransactionsListQueryHandler>();
        services.AddScoped<GetCategoriesQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapTransactions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/transactions", async (
            Guid[]? budgetId, DateOnly? from, DateOnly? to,
            Guid? categoryId, string? sort, string? search,
            TransactionStatus[]? status, decimal? amountFrom, decimal? amountTo,
            GetTransactionsListQueryHandler handler, CancellationToken ct,
            bool uncategorized = false, TransactionDirection direction = TransactionDirection.All,
            int page = 1, int pageSize = 10, bool desc = true) =>
        {
            var filter = new TransactionFilterRequestDto(
                budgetId, from, to, categoryId, uncategorized, direction,
                status, amountFrom, amountTo, search);

            return Results.Ok(await handler.HandleAsync(filter, page, pageSize, sort, desc, ct));
        })
        .WithName("ListTransactions")
        .Produces<TransactionListResponseDto>()
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/categories", async (GetCategoriesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("ListCategories")
            .Produces<IReadOnlyList<CategoryOptionResponseDto>>();

        app.MapPatch("/api/transactions", async (
            UpdateTransactionsRequestDto request, UpdateTransactionsCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("UpdateTransactions")
            .Produces<IReadOnlyList<TransactionListItemResponseDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/transactions/bulk-delete", async (
            BulkDeleteRequestDto request, BulkDeleteTransactionsCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("BulkDeleteTransactions")
            .Produces<BulkActionResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/transactions/bulk-set-category", async (
            BulkSetCategoryRequestDto request, BulkSetTransactionCategoryCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("BulkSetCategoryTransactions")
            .Produces<BulkActionResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/transactions/bulk-large-expense", async (
            BulkSetLargeExpenseRequestDto request, BulkSetTransactionLargeExpenseCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("BulkSetLargeExpenseTransactions")
            .Produces<BulkActionResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
