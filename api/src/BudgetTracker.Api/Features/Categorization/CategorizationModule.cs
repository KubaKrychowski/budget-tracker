using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;

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

        services.AddScoped<RuleCategorizer>();
        services.AddScoped<MlCategorizer>();
        services.AddScoped<ICategorizer, HybridCategorizer>();
        services.AddSingleton<ModelStore>();
        services.AddScoped<TrainingSetBuilder>();
        services.AddScoped<CategoryRuleLookup>();

        services.AddScoped<TrainCategoryModelCommandHandler>();
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

    public static IEndpointRouteBuilder MapCategorization(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categorization/training-set", async (
            GetTrainingSetQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("GetTrainingSet")
            .Produces<TrainingSetOverviewResponseDto>();

        app.MapPost("/api/categorization/train", async (
            TrainCategoryModelCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("TrainCategoryModel")
            .Produces<TrainingReportResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        app.MapPost("/api/categorization/recategorize", async (
            RecategorizeTransactionsCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("RecategorizeTransactions")
            .Produces<RecategorizeReportResponseDto>()
            .Produces(StatusCodes.Status409Conflict);

        app.MapPost("/api/categorization/activate", (
            ActivateModelRequestDto request, ActivateCategoryModelCommandHandler handler) =>
        {
            handler.Handle(request.Version);
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
}
