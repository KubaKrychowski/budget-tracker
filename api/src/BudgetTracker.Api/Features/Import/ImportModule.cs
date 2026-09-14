using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Import.Commands;
using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Exceptions;
using BudgetTracker.Api.Features.Import.Models;
using BudgetTracker.Api.Features.Import.Queries;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Infrastructure.Exceptions;
using BudgetTracker.Api.Resources;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Api.Features.Import;

/// <summary>Rejestracja DI i endpointy importu wyciągów — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Nowy bank = kolejna rejestracja <see cref="IStatementParser"/> w <c>AddImport</c> i nowa klasa. Handlery
/// importu zostają nietknięte — to kryterium akceptacji z issue, nie tylko preferencja.</item>
/// <item>Podgląd potrzebuje <c>budgetId</c>, bo deduplikacja działa w obrębie budżetu: ten sam wyciąg wgrany
/// na inny budżet to nowe dane, nie duplikaty.</item>
/// <item>Zły format pliku to błąd WEJŚCIA, nie serwera — 400 z komunikatem z zasobów, nigdy 500. Kryterium
/// akceptacji z issue: „bez crasha".</item>
/// <item>⚠️ Świadome odstępstwo od <c>DomainExceptionHandler</c>, który mapuje <see cref="EntityNotFoundException"/>
/// na 404. Tutaj budżet przychodzi w PARAMETRZE albo w ciele, nie w adresie zasobu — 404 mówiłby, że nie ma endpointu
/// importu, a nie że wskazany budżet zniknął (np. między krokiem 1 a zatwierdzeniem). Stąd 400 z komunikatem.
/// Wyłączony budżet (409) obsługuje już middleware — tam nie ma czego doprecyzowywać.</item>
/// <item>Zapis przyjmuje WIERSZE, nie plik — użytkownik mógł w kroku 3 usunąć pozycje i poprawić kategorie,
/// a ponowne parsowanie pliku wyrzuciłoby te zmiany.</item>
/// </list>
/// </remarks>
public static class ImportModule
{
    /// <summary>
    /// Górny limit rozmiaru pliku. Wyciąg z 20 miesięcy waży ok. 360 kB, więc 10 MB
    /// jest z ogromnym zapasem, a chroni przed wysłaniem czegoś zupełnie innego.
    /// </summary>
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        services.AddScoped<ImportBudgetLookup>();
        services.AddScoped<ExistingTransactionKeys>();
        services.AddScoped<ImportConfidenceThreshold>();

        services.AddScoped<IStatementParser, PkoParser>();
        services.AddScoped<IStatementParser, MBankParser>();

        services.AddScoped<GetImportSourcesQueryHandler>();
        services.AddScoped<GetImportPreviewQueryHandler>();
        services.AddScoped<CommitImportCommandHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapImport(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/import/sources", async (GetImportSourcesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
        .WithName("ImportSources")
        .Produces<ImportSourcesResponseDto>();

        app.MapPost("/api/import/preview", async (
            IFormFile file,
            string bank,
            Guid budgetId,
            IEnumerable<IStatementParser> parsers,
            GetImportPreviewQueryHandler handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken ct) =>
        {
            if (file.Length == 0) return BadRequest(localizer, "Import_EmptyFile");
            if (file.Length > MaxFileSizeBytes) return BadRequest(localizer, "Import_FileTooLarge");

            var parser = parsers.FirstOrDefault(p =>
                string.Equals(p.BankKey, bank, StringComparison.OrdinalIgnoreCase));

            if (parser is null) return BadRequest(localizer, "Import_UnknownBank");

            IReadOnlyList<ParsedRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = await parser.ParseAsync(stream, ct);
            }
            catch (StatementFormatException ex)
            {
                return BadRequest(localizer, ex.ResourceKey);
            }

            try
            {
                return Results.Ok(await handler.HandleAsync(rows, budgetId, ct));
            }
            catch (EntityNotFoundException)
            {
                return BadRequest(localizer, "Import_UnknownBudget");
            }
        })
        .WithName("ImportPreviewResponseDto")
        .DisableAntiforgery();

        app.MapPost("/api/import", async (
            CommitRequestDto request,
            CommitImportCommandHandler handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken ct) =>
        {
            if (request.Rows.Count == 0) return BadRequest(localizer, "Import_NoRows");

            try
            {
                return Results.Ok(await handler.HandleAsync(request, ct));
            }
            catch (EntityNotFoundException)
            {
                return BadRequest(localizer, "Import_UnknownBudget");
            }
        })
        .WithName("ImportStatement");

        return app;
    }

    private static IResult BadRequest(IStringLocalizer<SharedResource> localizer, string resourceKey) =>
        Results.BadRequest(new { error = localizer[resourceKey].Value });
}
