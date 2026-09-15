using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Exceptions;
using BudgetTracker.Api.Features.Cli.Services;
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

    /// <summary>
    /// Komendy CLI (issue #25). Bez uploadu strumienia: plik idzie jako <c>--content</c> zakodowany base64 —
    /// serwer nie dostaje nowej ścieżki czytania z dysku, a wynik jest tym samym <c>byte[]</c>, który dziś
    /// dostaje parser z <see cref="IFormFile.OpenReadStream"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ REST celowo zamienia <see cref="EntityNotFoundException"/> (nieznany budżet) na 400, nie 404 —
    /// budżet przychodzi w parametrze/ciele, nie w adresie zasobu (patrz doc klasy). Dispatcher CLI tego
    /// rozróżnienia nie zna (dla niego to zwykły wyjątek domenowy → 404 przez <c>DomainExceptionHandler</c>),
    /// więc obie komendy łapią go tu i zamieniają na <see cref="CliArgumentException"/>, żeby zachowanie
    /// zostało spójne z REST.
    /// </remarks>
    public static CliCommandRegistry MapImportCli(this CliCommandRegistry registry)
    {
        registry.Register("import", "sources", "Banki, budżety i kategorie potrzebne do kroku importu.",
            "import sources", [],
            async (sp, _, ct) => await sp.GetRequiredService<GetImportSourcesQueryHandler>().HandleAsync(ct));

        registry.Register("import", "preview", "Parsuje wyciąg i pokazuje, co zaimportowałby zapis — bez zapisu.",
            "import preview --bank <klucz> --budget-id <guid> --content <base64>",
            [
                CliFlag.Required("bank", "Klucz banku z „import sources” (np. pko, mbank)."),
                CliFlag.Required("budget-id", "BusinessId budżetu, na który trafiłby import."),
                CliFlag.Required("content", "Zawartość pliku CSV zakodowana base64."),
            ],
            async (sp, args, ct) =>
            {
                var bank = args.GetRequiredFlag("bank");
                var parser = sp.GetServices<IStatementParser>()
                    .FirstOrDefault(p => string.Equals(p.BankKey, bank, StringComparison.OrdinalIgnoreCase))
                    ?? throw new CliArgumentException($"Nieznany bank „{bank}”. Sprawdź „import sources”.");

                var bytes = DecodeBase64(args, "content");

                IReadOnlyList<ParsedRow> rows;
                try
                {
                    using var stream = new MemoryStream(bytes);
                    rows = await parser.ParseAsync(stream, ct);
                }
                catch (StatementFormatException ex)
                {
                    throw new CliArgumentException(ex.Message);
                }

                try
                {
                    return await sp.GetRequiredService<GetImportPreviewQueryHandler>()
                        .HandleAsync(rows, args.GetRequiredGuidFlag("budget-id"), ct);
                }
                catch (EntityNotFoundException)
                {
                    throw new CliArgumentException("Nieznany --budget-id.");
                }
            });

        registry.Register("import", "run", "Zapisuje przejrzane wiersze (wynik „import preview”, ewentualnie poprawiony).",
            "import run --budget-id <guid> --bank <klucz> --file-name <nazwa> --rows-json <json>",
            [
                CliFlag.Required("budget-id", "BusinessId budżetu docelowego."),
                CliFlag.Required("bank", "Klucz banku — tylko do zapisania w historii importu."),
                CliFlag.Required("file-name", "Nazwa pliku — tylko do zapisania w historii importu."),
                CliFlag.Required("rows-json",
                    "Tablica JSON wierszy z „import preview” (po usunięciach/korektach): "
                    + "[{\"date\":\"RRRR-MM-DD\",\"amount\":0,\"description\":\"...\",\"transactionType\":\"...\","
                    + "\"externalReference\":null,\"categoryId\":\"guid|null\",\"confidence\":null,\"edited\":false}]."),
            ],
            async (sp, args, ct) =>
            {
                var request = new CommitRequestDto(
                    args.GetRequiredGuidFlag("budget-id"),
                    args.GetRequiredFlag("bank"),
                    args.GetRequiredFlag("file-name"),
                    args.GetRequiredJsonFlag<IReadOnlyList<CommitRowRequestDto>>("rows-json"));

                if (request.Rows.Count == 0) throw new CliArgumentException("--rows-json: pusta lista wierszy.");

                try
                {
                    return await sp.GetRequiredService<CommitImportCommandHandler>().HandleAsync(request, ct);
                }
                catch (EntityNotFoundException)
                {
                    throw new CliArgumentException("Nieznany --budget-id.");
                }
            });

        return registry;
    }

    private static byte[] DecodeBase64(CliArgs args, string flag)
    {
        try
        {
            return Convert.FromBase64String(args.GetRequiredFlag(flag));
        }
        catch (FormatException)
        {
            throw new CliArgumentException($"--{flag}: niepoprawny base64.");
        }
    }
}
