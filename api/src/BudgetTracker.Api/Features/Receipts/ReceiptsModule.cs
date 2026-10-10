using Azure.AI.DocumentIntelligence;
using Azure.Core;
using BudgetTracker.Api.Features.Receipts.Commands;
using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Queries;
using BudgetTracker.Api.Features.Receipts.Services;

namespace BudgetTracker.Api.Features.Receipts;

/// <summary>Rejestracja DI i endpointy paragonów — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Wybór silnika OCR jest w konfiguracji: <c>Receipts:Endpoint</c> = Azure Document Intelligence, przy
/// <c>Demo:Seed</c> bez adresu — czytnik demo, w pozostałych przypadkach czytnik „niedostępny” (503), a reszta API działa.</item>
/// <item>Wgranie paragonu i jego zapis to dwa kroki, bo między nimi użytkownik poprawia pola i wybiera transakcję;
/// kandydaci liczą się osobnym zapytaniem, żeby zmiana sumy na ekranie weryfikacji nie wołała OCR drugi raz.</item>
/// <item>Endpointy NIE łapią wyjątków — mapuje je <c>DomainExceptionHandler</c>.</item>
/// </list>
/// </remarks>
public static class ReceiptsModule
{
    public static IServiceCollection AddReceipts(this IServiceCollection services)
    {
        services.AddScoped<IReceiptReader>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            if (configuration["Receipts:Endpoint"] is { Length: > 0 } endpoint)
            {
                return new AzureReceiptReader(
                    new DocumentIntelligenceClient(new Uri(endpoint), sp.GetRequiredService<TokenCredential>()));
            }

            return configuration.GetValue<bool>("Demo:Seed")
                ? new DemoReceiptReader(sp.GetRequiredService<TimeProvider>())
                : new UnconfiguredReceiptReader();
        });
        services.AddScoped<ReceiptFiles>();

        services.AddScoped<UploadReceiptCommandHandler>();
        services.AddScoped<SaveReceiptCommandHandler>();
        services.AddScoped<DeleteReceiptCommandHandler>();

        services.AddScoped<GetReceiptCandidatesQueryHandler>();
        services.AddScoped<GetReceiptFileQueryHandler>();
        services.AddScoped<GetTransactionReceiptsQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapReceipts(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/receipts", async (
            IFormFile file, UploadReceiptCommandHandler handler, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            return Results.Ok(await handler.HandleAsync(stream, file.FileName, file.ContentType, file.Length, ct));
        })
        .WithName("UploadReceipt")
        .DisableAntiforgery()
        .Produces<ReceiptReadResponseDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/receipts/candidates", async (
            Guid[]? budgetId, decimal? total, DateOnly? date,
            GetReceiptCandidatesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, total, date, ct)))
            .WithName("ListReceiptCandidates")
            .Produces<IReadOnlyList<ReceiptCandidateResponseDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/receipts/{id:guid}", async (
            Guid id, SaveReceiptRequestDto request, SaveReceiptCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("SaveReceipt")
            .Produces<ReceiptResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/receipts/{id:guid}", async (
            Guid id, DeleteReceiptCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteReceipt")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/receipts/{id:guid}/file", async (
            Guid id, GetReceiptFileQueryHandler handler, CancellationToken ct) =>
        {
            var (content, contentType, fileName) = await handler.HandleAsync(id, ct);
            return Results.File(content, contentType, fileName);
        })
        .WithName("GetReceiptFile")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/transactions/{id:guid}/receipts", async (
            Guid id, GetTransactionReceiptsQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)))
            .WithName("ListTransactionReceipts")
            .Produces<IReadOnlyList<ReceiptResponseDto>>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
