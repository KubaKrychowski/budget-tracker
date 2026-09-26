using Microsoft.Extensions.Options;
using System.Globalization;
using Azure.Identity;
using BudgetTracker.Api.Features.Admin;
using BudgetTracker.Api.Features.Budgets;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Features.Dashboard;
using BudgetTracker.Api.Features.Import;
using BudgetTracker.Api.Features.Limits;
using BudgetTracker.Api.Features.EpisodicOrders;
using BudgetTracker.Api.Features.Savings;
using BudgetTracker.Api.Features.Search;
using BudgetTracker.Api.Features.StandingOrders;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Jobs;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLocalization();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<SoftDeleteInterceptor>();
builder.Services.AddScoped<RlsSessionInterceptor>();
builder.Services.AddScoped<RlsTransactionInterceptor>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .AddInterceptors(
            sp.GetRequiredService<SoftDeleteInterceptor>(),
            sp.GetRequiredService<RlsSessionInterceptor>(),
            sp.GetRequiredService<RlsTransactionInterceptor>()));

// Resource server: waliduje JWT wystawione przez BudgetTracker.Identity przez jego JWKS
// (SetIssuer + UseSystemNetHttp), bez wspólnej bazy z serwerem tożsamości.
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    AdminModule.ConfigureAdminPolicy(options);
});

builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration[IdentityServerDefaults.IssuerConfigKey]
            ?? throw new InvalidOperationException($"Brak konfiguracji {IdentityServerDefaults.IssuerConfigKey}."));
        options.AddAudiences(IdentityServerDefaults.ApiAudience);
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddJobs(builder.Configuration);

builder.Services.AddAdmin();
builder.Services.AddDashboard();
builder.Services.AddCategorization(builder.Configuration);
builder.Services.AddImport();
builder.Services.AddBudgets(builder.Configuration);
builder.Services.AddTransactions();
builder.Services.AddSavings();
builder.Services.AddLimits();
builder.Services.AddStandingOrders();
builder.Services.AddEpisodicOrders();
builder.Services.AddSearch();

builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(new Uri(builder.Configuration["Storage:BlobServiceUri"]!));
    clients.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeManagedIdentityCredential = builder.Environment.IsDevelopment(),
        ExcludeWorkloadIdentityCredential = true,
    }));
});

// Originy frontu z konfiguracji, nie z kodu: adres i port zależą od środowiska.
//
// ⚠️ Ta polityka obowiązuje w KAŻDYM środowisku, nie tylko w Development. Do 2026-09-25 `UseCors` stało
// wyłącznie w gałęzi deweloperskiej, co było poprawne dopóki front i API chodziły na jednym localhoście
// za wspólnym proxy. Na Azure front stoi na Static Web Apps (`*.azurestaticapps.net`), a API na App
// Service (`*.azurewebsites.net`) — to dwa różne originy, więc bez tej polityki przeglądarka odrzuca
// każde żądanie aplikacji, mimo że token jest poprawny. Plan Free Static Web Apps nie ma „linked
// backend", więc odwrotne proxy po stronie SWA nie jest tu wyjściem.
//
// Pusta lista originów nie otwiera niczego: `WithOrigins()` bez wartości nie dopasuje żadnego żądania.
const string frontendCors = "frontend";
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(frontendCors, policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("pl"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("pl"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

using (var baseline = app.Services.CreateScope())
{
    var baselineDb = baseline.ServiceProvider.GetRequiredService<AppDbContext>();

    // Reguły bazowe są WSPÓLNE (UserId = pusty Guid), a RLS pozwala roli budget_app zapisywać tylko własne wiersze —
    // seed bez kontekstu użytkownika działa więc na budget_jobs (BYPASSRLS), w jednej transakcji, jak BudgetPurger.
    await using var baselineTransaction = await baselineDb.Database.BeginTransactionAsync();
    await baselineDb.Database.ExecuteSqlRawAsync("SET LOCAL ROLE budget_jobs");

    await BaselineSeed.SeedAsync(baselineDb);

    await LocalRulesSeed.SeedAsync(
        baselineDb,
        baseline.ServiceProvider.GetRequiredService<IOptions<CategorizationOptions>>().Value.LocalRulesPath,
        baseline.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(LocalRulesSeed)));

    await baselineTransaction.CommitAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    using var scope = app.Services.CreateScope();
    var seedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedClock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

    // Seed startuje bez kontekstu żądania HTTP (jak BudgetPurger) — pod RLS w Postgresie strażnik
    // "czy transakcje już są" w Dev/DemoSeed widziałby zawsze puste (nikt nie ustawił
    // app.current_user_id), więc seed próbowałby dosypać dane po raz drugi. `budget_jobs` na czas
    // TEJ jednej transakcji, tak samo jak w BudgetPurger.PurgeAsync.
    await using (var seedTransaction = await seedDb.Database.BeginTransactionAsync())
    {
        await seedDb.Database.ExecuteSqlRawAsync("SET LOCAL ROLE budget_jobs");

        if (app.Configuration.GetValue<bool>("Demo:Seed")) await DemoSeed.SeedAsync(seedDb, seedClock);
        else await DevSeed.SeedAsync(seedDb, seedClock);

        await seedTransaction.CommitAsync();
    }
}

app.UseExceptionHandler();

// W Development nie ma czego przekierowywać: API słucha wyłącznie na https (launchSettings). Nie dokładaj
// tu drugiego, http-owego adresu: przekierowanie 307 z http na https to SKOK MIĘDZY ORIGINAMI, na którym
// przeglądarka CELOWO zdejmuje nagłówek Authorization (spec fetch), więc token ginie i żądanie wygląda
// jak niezalogowane (401), mimo że front poprawnie go wysłał.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Przed uwierzytelnianiem: odpowiedź na zapytanie wstępne (preflight) jest z definicji bez tokenu, więc
// za `UseAuthorization` dostałaby 401 i przeglądarka nie puściłaby właściwego żądania.
app.UseCors(frontendCors);

app.UseAuthentication();
app.UseAuthorization();

app.UseJobsDashboard();
app.UseBudgetJobs();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/db", async (AppDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { database = "up" })
            : Results.Problem("Brak połączenia z Postgresem — czy `docker compose up -d db` działa?"))
    .AllowAnonymous();

// Wszystkie endpointy danych idą przez grupę z filtrem transakcji RLS: uwierzytelnione żądanie dostaje jedną
// transakcję, w której SET LOCAL app.current_user_id dojeżdża do zapytań (naprawa pod poolerem — patrz
// RlsTransactionEndpointFilter). /health i /health/db zostają poza grupą (anonimowe, nie dotykają danych właściciela).
var api = app.MapGroup("").AddEndpointFilter<RlsTransactionEndpointFilter>();

// ⚠️ Admin POZA filtrem: `OwnerDataService` sam zarządza transakcją i przełącza rolę na `budget_jobs`
// (BYPASSRLS) przez `SET LOCAL ROLE`. Reużycie transakcji żądania zostawiłoby resztę żądania z tą rolą,
// a jego własne `BeginTransactionAsync` wywaliłoby się na zagnieżdżeniu. Admin nie potrzebuje
// `app.current_user_id` — czyta z jawnym `.Where(UserId == ownerId)` i tak omija RLS.
app.MapAdmin();

api.MapDashboard();
api.MapCategorization();
api.MapSavings();
api.MapLimits();
api.MapStandingOrders();
api.MapEpisodicOrders();
api.MapSearch();
api.MapImport();
api.MapBudgets();
api.MapTransactions();

// Wydatki CLI (issue #25) — każdy Map<Feature>Cli() dopisuje swoje komendy do wspólnego rejestru,
// dokładnie tak jak lista app.Map<Feature>() wyżej dopisuje endpointy REST.
var cli = new CliCommandRegistry()
    .MapDashboardCli()
    .MapCategorizationCli()
    .MapImportCli()
    .MapLimitsCli()
    .MapStandingOrdersCli()
    .MapEpisodicOrdersCli()
    .MapSavingsCli()
    .MapBudgetsCli()
    .MapTransactionsCli()
    .MapSearchCli();
api.MapCli(cli);

app.Run();

public partial class Program;
