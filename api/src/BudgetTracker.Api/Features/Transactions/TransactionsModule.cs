using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
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

        services.AddScoped<GetTransactionsListQueryHandler>();
        services.AddScoped<GetCategoriesQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapTransactions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/transactions", async (
            Guid[]? budgetId, DateOnly? from, DateOnly? to,
            Guid? categoryId, string? sort, string? search,
            TransactionStatus[]? status, decimal? amountFrom, decimal? amountTo, Guid? standingOrderId,
            GetTransactionsListQueryHandler handler, CancellationToken ct,
            bool uncategorized = false, TransactionDirection direction = TransactionDirection.All,
            int page = 1, int pageSize = 10, bool desc = true) =>
        {
            var filter = new TransactionFilterRequestDto(
                budgetId, from, to, categoryId, uncategorized, direction,
                status, amountFrom, amountTo, search, standingOrderId);

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

        return app;
    }

    /// <summary>Komendy CLI (issue #25) — te same handlery co endpointy REST wyżej.</summary>
    public static CliCommandRegistry MapTransactionsCli(this CliCommandRegistry registry)
    {
        registry.Register("transaction", "list", "Lista transakcji z filtrami, sortowaniem i stronicowaniem.",
            "transaction list [--budget-id <guid,...>] [--from <data>] [--to <data>] [--category-id <guid>] "
            + "[--uncategorized] [--direction All|Expense|Income] [--status Imported,...] [--amount-from <kwota>] "
            + "[--amount-to <kwota>] [--search <fraza>] [--standing-order-id <guid>] [--sort date|amount] "
            + "[--desc] [--page <n>] [--page-size <n>]",
            [
                CliFlag.Optional("budget-id", "Lista BusinessId budżetów po przecinku; puste = budżet domyślny."),
                CliFlag.Optional("from", "Data od (RRRR-MM-DD)."),
                CliFlag.Optional("to", "Data do (RRRR-MM-DD)."),
                CliFlag.Optional("category-id", "BusinessId kategorii."),
                CliFlag.Optional("uncategorized", "Tylko bez kategorii."),
                CliFlag.Optional("direction", "All (domyślnie) / Expense / Income."),
                CliFlag.Optional("status", "Lista statusów po przecinku (Imported, AutoCategorized, PendingReview, ...)."),
                CliFlag.Optional("amount-from", "Dolna granica kwoty bezwzględnej."),
                CliFlag.Optional("amount-to", "Górna granica kwoty bezwzględnej."),
                CliFlag.Optional("search", "Fraza w opisie."),
                CliFlag.Optional("standing-order-id", "Tylko transakcje przypięte do tego zlecenia stałego."),
                CliFlag.Optional("sort", "„date” (domyślnie) albo „amount”."),
                CliFlag.Optional("desc", "Malejąco (domyślnie true)."),
                CliFlag.Optional("page", "Numer strony, domyślnie 1."),
                CliFlag.Optional("page-size", "Wierszy na stronę, domyślnie 10, maks. 200."),
            ],
            async (sp, args, ct) =>
            {
                var filter = new TransactionFilterRequestDto(
                    args.GetGuidArrayFlag("budget-id"),
                    args.GetDateFlag("from"),
                    args.GetDateFlag("to"),
                    args.GetGuidFlag("category-id"),
                    args.GetBoolFlag("uncategorized"),
                    args.GetEnumFlag("direction", TransactionDirection.All),
                    args.GetEnumArrayFlag<TransactionStatus>("status"),
                    args.GetDecimalFlag("amount-from"),
                    args.GetDecimalFlag("amount-to"),
                    args.GetFlag("search"),
                    args.GetGuidFlag("standing-order-id"));

                var handler = sp.GetRequiredService<GetTransactionsListQueryHandler>();
                return await handler.HandleAsync(
                    filter,
                    args.GetIntFlag("page", 1),
                    args.GetIntFlag("page-size", 10),
                    args.GetFlag("sort"),
                    args.GetBoolFlag("desc", true),
                    ct);
            });

        registry.Register("category", "list", "Słownik kategorii, alfabetycznie.", "category list", [],
            async (sp, _, ct) => await sp.GetRequiredService<GetCategoriesQueryHandler>().HandleAsync(ct));

        registry.Register("transaction", "update", "Zapisuje edycje wierszy (pojedynczą albo masową, tym samym mechanizmem).",
            "transaction update --edits-json <json> [--budget-id <guid,...>]",
            [
                CliFlag.Required("edits-json",
                    "Tablica JSON: [{\"id\":\"guid\",\"date\":\"RRRR-MM-DD\",\"description\":\"...\",\"amount\":0,\"categoryId\":\"guid|null\"}]."),
                CliFlag.Optional("budget-id", "Zasięg budżetów — wiersz spoza nich daje 404, jak w REST."),
            ],
            async (sp, args, ct) =>
            {
                var request = new UpdateTransactionsRequestDto(
                    args.GetRequiredJsonFlag<IReadOnlyList<TransactionEditRequestDto>>("edits-json"),
                    args.GetGuidArrayFlag("budget-id"));
                return await sp.GetRequiredService<UpdateTransactionsCommandHandler>().HandleAsync(request, ct);
            });

        registry.Register("transaction", "bulk-delete", "Masowe usunięcie transakcji z zasięgu zaznaczenia albo filtra.",
            "transaction bulk-delete --selection-json <json>",
            [CliFlag.Required("selection-json",
                "{\"ids\":[\"guid\",...]|null,\"filter\":{... jak w „transaction list”}|null}.")],
            async (sp, args, ct) =>
            {
                var selection = args.GetRequiredJsonFlag<TransactionSelectionRequestDto>("selection-json");
                return await sp.GetRequiredService<BulkDeleteTransactionsCommandHandler>()
                    .HandleAsync(new BulkDeleteRequestDto(selection), ct);
            });

        registry.Register("transaction", "bulk-set-category", "Masowe ustawienie kategorii z zasięgu zaznaczenia albo filtra.",
            "transaction bulk-set-category --selection-json <json> --category-id <guid>",
            [
                CliFlag.Required("selection-json",
                    "{\"ids\":[\"guid\",...]|null,\"filter\":{... jak w „transaction list”}|null}."),
                CliFlag.Required("category-id", "BusinessId docelowej kategorii."),
            ],
            async (sp, args, ct) =>
            {
                var selection = args.GetRequiredJsonFlag<TransactionSelectionRequestDto>("selection-json");
                var request = new BulkSetCategoryRequestDto(selection, args.GetRequiredGuidFlag("category-id"));
                return await sp.GetRequiredService<BulkSetTransactionCategoryCommandHandler>()
                    .HandleAsync(request, ct);
            });

        return registry;
    }
}
