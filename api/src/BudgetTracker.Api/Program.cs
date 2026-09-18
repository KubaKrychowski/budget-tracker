using Microsoft.Extensions.Options;
using System.Globalization;
using BudgetTracker.Api.Features.Budgets;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Features.Dashboard;
using BudgetTracker.Api.Features.Import;
using BudgetTracker.Api.Features.Limits;
using BudgetTracker.Api.Features.EpisodicOrders;
using BudgetTracker.Api.Features.Savings;
using BudgetTracker.Api.Features.StandingOrders;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Jobs;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLocalization();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<SoftDeleteInterceptor>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>()));

// Resource server: waliduje JWT wystawione przez BudgetTracker.Identity przez jego JWKS
// (SetIssuer + UseSystemNetHttp), bez wspólnej bazy z serwerem tożsamości.
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

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

builder.Services.AddDashboard();
builder.Services.AddCategorization(builder.Configuration);
builder.Services.AddImport();
builder.Services.AddBudgets(builder.Configuration);
builder.Services.AddTransactions();
builder.Services.AddSavings();
builder.Services.AddLimits();
builder.Services.AddStandingOrders();
builder.Services.AddEpisodicOrders();

const string devCors = "dev-frontend";
builder.Services.AddCors(options =>
    options.AddPolicy(devCors, policy => policy
        .WithOrigins("http://localhost:4200")
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
    await BaselineSeed.SeedAsync(baselineDb);

    await LocalRulesSeed.SeedAsync(
        baselineDb,
        baseline.ServiceProvider.GetRequiredService<IOptions<CategorizationOptions>>().Value.LocalRulesPath,
        baseline.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(LocalRulesSeed)));
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseCors(devCors);

    using var scope = app.Services.CreateScope();
    var seedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedClock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

    if (app.Configuration.GetValue<bool>("Demo:Seed")) await DemoSeed.SeedAsync(seedDb, seedClock);
    else await DevSeed.SeedAsync(seedDb, seedClock);
}

app.UseExceptionHandler();

// W Development NIE przekierowujemy na https: gdy Rider odpala profil z parą portów
// http/https, przekierowanie 307 z http (tam, gdzie celuje proxy Angulara) na https to
// SKOK MIĘDZY ORIGINAMI — przeglądarka na takim skoku CELOWO zdejmuje nagłówek
// Authorization (spec fetch), więc token z requestu ginie i finalne żądanie na https
// wygląda jak niezalogowane (401), mimo że front poprawnie go wysłał.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

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

app.MapDashboard();
app.MapCategorization();
app.MapSavings();
app.MapLimits();
app.MapStandingOrders();
app.MapEpisodicOrders();
app.MapImport();
app.MapBudgets();
app.MapTransactions();

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
    .MapTransactionsCli();
app.MapCli(cli);

app.Run();

public partial class Program;
