using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Jobs;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;

namespace BudgetTracker.Api.Features.Categorization;

/// <summary>Rejestracja DI i endpointy kategoryzacji — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Zarejestrowany jest HYBRYDOWY <see cref="ICategorizer"/> — reszta aplikacji nie wie, czy odpowiedź
/// przyszła z reguły, czy z modelu.</item>
/// <item><see cref="ModelStore"/> jest SINGLETONEM, w odróżnieniu od reszty: trzyma blokadę „trwa import / trwa
/// trening", czyli stan współdzielony między żądaniami. Per scope byłaby to blokada, która nikogo nie blokuje.</item>
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c> w jednym miejscu.
/// Stąd <c>Produces</c> jako jedyny ślad po kontrakcie.</item>
/// <item>Trening jest operacją administracyjną i długotrwałą, więc świadomie NIE jest częścią importu — inaczej
/// pierwszy wgrany plik czekałby na uczenie modelu. Ekran ma do niego zachęcać liczbą nowych poprawek,
/// nie odpalać go sam.</item>
/// <item>Przeliczenie kategorii NIE jest częścią treningu — trening produkuje model, przeliczenie zmienia dane.
/// Sklejenie ich znaczyłoby, że nie da się nauczyć modelu bez przepisania historii, a to dwie różne zgody
/// użytkownika.</item>
/// <item>Reguły przez API domykają deklarację z docu <c>CategoryRule</c>: „trzymana w bazie, nie w kodzie, żeby
/// dołożenie sprzedawcy nie wymagało wdrożenia". ⚠️ To nie jest wygoda, tylko kwestia prywatności: dopóki jedynym
/// domem reguły był kod źródłowy (<c>BaselineSeed</c>), każda korekta kategoryzatora zapisywała w repozytorium
/// fragment prywatnego wyciągu. Zapis przez API sprawia, że taka poprawka zostaje w bazie użytkownika.</item>
/// </list>
/// </remarks>
public static class CategorizationModule
{
    public static IServiceCollection AddCategorization(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<CategorizationOptions>(config.GetSection(CategorizationOptions.SectionName));

        // Bajty modelu i policzona bramka zyja NA WERSJE MODELU, wiec potrzebuja pamieci poza zakresem zadania.
        services.AddMemoryCache();

        services.AddScoped<CategoryModelSource>();
        services.AddScoped<VocabularyGate>();
        services.AddScoped<RuleCategorizer>();
        services.AddScoped<MlCategorizer>();
        services.AddScoped<ICategorizer, HybridCategorizer>();
        services.AddScoped<ModelStore>();
        services.AddScoped<TrainingSetBuilder>();
        services.AddScoped<CategoryRuleLookup>();

        services.AddScoped<GetTrainingStatusQueryHandler>();
        services.AddScoped<GetTrainingStatusQueryHandler>();
        services.AddScoped<TrainCategoryModelCommandHandler>();
        services.AddScoped<TrainCategoryModelJob>();
        services.AddScoped<RecategorizeTransactionsCommandHandler>();
        services.AddScoped<ActivateCategoryModelCommandHandler>();
        services.AddScoped<CreateCategoryRuleCommandHandler>();
        services.AddScoped<UpdateCategoryRuleCommandHandler>();
        services.AddScoped<DeleteCategoryRuleCommandHandler>();

        services.AddScoped<GetTrainingSetQueryHandler>();
        services.AddScoped<GetCategoryRulesQueryHandler>();
        services.AddScoped<PreviewCategoryRuleQueryHandler>();

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

    public static IEndpointRouteBuilder MapCategorization(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categorization/training-set", async (
            GetTrainingSetQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("GetTrainingSet")
            .Produces<TrainingSetOverviewResponseDto>();

        app.MapPost("/api/categorization/train", (
            TrainCategoryModelCommandHandler handler) =>
            Results.Ok(handler.Handle()))
            .WithName("TrainCategoryModel")
            .Produces<TrainingQueuedResponseDto>()
            .Produces(StatusCodes.Status401Unauthorized);

        // Wynik zgłoszenia z POST wyżej — ekran odpytuje, dopóki nie ma wersji.
        // ⚠️ Zawsze 200: „jeszcze nie ma wyniku" to normalna odpowiedź, a nie brak zasobu. Cudze i zmyślone
        // zgłoszenie dają to samo, bo wersje zawęża filtr własnościowy.
        app.MapGet("/api/categorization/train/{jobId}", async (
            string jobId, GetTrainingStatusQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(jobId, ct)))
            .WithName("GetTrainingStatus")
            .Produces<TrainingStatusResponseDto>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/categorization/recategorize", async (
            RecategorizeTransactionsCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("RecategorizeTransactions")
            .Produces<RecategorizeReportResponseDto>()
            .Produces(StatusCodes.Status409Conflict);

        app.MapPost("/api/categorization/activate", async (
            ActivateModelRequestDto request, ActivateCategoryModelCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(request.VersionId, ct);
            return Results.NoContent();
        })
            .WithName("ActivateCategoryModel")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapGet("/api/categorization/rules", async (
            GetCategoryRulesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("GetCategoryRules")
            .Produces<IReadOnlyList<CategoryRuleResponseDto>>();

        // Podgląd stoi PRZED zapisem także w trasach, bo tak wygląda kolejność w kreatorze.
        // Czasownik POST, mimo że nic nie zapisuje: reguła jedzie w ciele, a nie w adresie.
        app.MapPost("/api/categorization/rules/preview", async (
            CategoryRuleRequestDto request, PreviewCategoryRuleQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("PreviewCategoryRule")
            .Produces<RulePreviewResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/categorization/rules", async (
            CategoryRuleRequestDto request, CreateCategoryRuleCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("CreateCategoryRule")
            .Produces<CategoryRuleResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/categorization/rules/{id:guid}", async (
            Guid id, CategoryRuleRequestDto request, UpdateCategoryRuleCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("UpdateCategoryRule")
            .Produces<CategoryRuleResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/categorization/rules/{id:guid}", async (
            Guid id, DeleteCategoryRuleCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("DeleteCategoryRule")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Komendy CLI (issue #25): rzeczownik <c>model</c> (trening/przeliczenie) i <c>rule</c> (reguły predykatu).</summary>
    public static CliCommandRegistry MapCategorizationCli(this CliCommandRegistry registry)
    {
        registry.Register("model", "training-set", "Podgląd danych, na których uczy się model.",
            "model training-set", [],
            async (sp, _, ct) => await sp.GetRequiredService<GetTrainingSetQueryHandler>().HandleAsync(ct));

        // CLI trenuje WPROST, z pominięciem kolejki: polecenie ma zwrócić metryki temu, kto je uruchomił,
        // a nie numer zadania do podglądania w panelu. Użytkownika bierze z własnego kontekstu.
        registry.Register("model", "train", "Trenuje nowy model na dotychczasowych poprawkach.",
            "model train", [],
            async (sp, _, ct) => await sp.GetRequiredService<TrainCategoryModelJob>()
                .RunAsync(sp.GetRequiredService<ICurrentUserAccessor>().UserId, context: null, ct));

        registry.Register("model", "recategorize", "Przelicza kategorie istniejących transakcji aktywnym modelem.",
            "model recategorize", [],
            async (sp, _, ct) =>
                await sp.GetRequiredService<RecategorizeTransactionsCommandHandler>().HandleAsync(ct));

        registry.Register("model", "activate", "Przywraca wskazaną wersję modelu jako aktywną.",
            "model activate --version <wersja>",
            [CliFlag.Required("version", "Wersja z listy „model training-set”.")],
            async (sp, args, ct) =>
            {
                await sp.GetRequiredService<ActivateCategoryModelCommandHandler>()
                    .HandleAsync(args.GetRequiredGuidFlag("version"), ct);
                return Task.FromResult<object?>(new { activated = true });
            });

        registry.Register("rule", "list", "Lista reguł predykatu, posortowana priorytetem.", "rule list", [],
            async (sp, _, ct) => await sp.GetRequiredService<GetCategoryRulesQueryHandler>().HandleAsync(ct));

        registry.Register("rule", "preview", "Podgląd, ile i które transakcje złapałaby reguła — bez zapisu.",
            RuleUsage("preview"), RuleFlags(),
            async (sp, args, ct) =>
                await sp.GetRequiredService<PreviewCategoryRuleQueryHandler>().HandleAsync(ToRequest(args), ct));

        registry.Register("rule", "create", "Tworzy regułę kategoryzacji.", RuleUsage("create"), RuleFlags(),
            async (sp, args, ct) =>
                await sp.GetRequiredService<CreateCategoryRuleCommandHandler>().HandleAsync(ToRequest(args), ct));

        registry.Register("rule", "update", "Zmienia regułę kategoryzacji.", RuleUsage("update <id>"), RuleFlags(),
            async (sp, args, ct) => await sp.GetRequiredService<UpdateCategoryRuleCommandHandler>()
                .HandleAsync(args.GetGuid(0), ToRequest(args), ct));

        registry.Register("rule", "delete", "Usuwa regułę kategoryzacji.", "rule delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<DeleteCategoryRuleCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        return registry;
    }

    private static string RuleUsage(string verbAndArgs) =>
        $"rule {verbAndArgs} --category-id <guid> --priority <n> [--pattern <fraza>] "
        + "[--transaction-type-pattern <fraza>] [--direction Any|Expense|Income] "
        + "[--min-amount <kwota>] [--max-amount <kwota>] [--note <tekst>]";

    private static IReadOnlyList<CliFlagDefinition> RuleFlags() =>
    [
        CliFlag.Required("category-id", "BusinessId docelowej kategorii."),
        CliFlag.Required("priority", "Niższa liczba = wyższy priorytet, wygrywa przy remisie."),
        CliFlag.Optional("pattern", "Fraza w opisie transakcji. Opcjonalne osobno, ale nie razem z pustym --transaction-type-pattern."),
        CliFlag.Optional("transaction-type-pattern", "Fraza w typie operacji z wyciągu."),
        CliFlag.Optional("direction", "Any (domyślnie) / Expense / Income."),
        CliFlag.Optional("min-amount", "Dolna granica kwoty."),
        CliFlag.Optional("max-amount", "Górna granica kwoty."),
        CliFlag.Optional("note", "Notatka — po co ta reguła istnieje."),
    ];

    private static CategoryRuleRequestDto ToRequest(CliArgs args) => new(
        args.GetFlag("pattern"),
        args.GetFlag("transaction-type-pattern"),
        args.GetEnumFlag("direction", RuleDirection.Any),
        args.GetRequiredGuidFlag("category-id"),
        args.GetRequiredIntFlag("priority"),
        args.GetDecimalFlag("min-amount"),
        args.GetDecimalFlag("max-amount"),
        args.GetFlag("note"));
}
