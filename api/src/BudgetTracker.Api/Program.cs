using Microsoft.Extensions.Options;
using System.Globalization;
using BudgetTracker.Api.Features.Budgets;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Dashboard;
using BudgetTracker.Api.Features.Import;
using BudgetTracker.Api.Features.Limits;
using BudgetTracker.Api.Features.Savings;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Jobs;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLocalization();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<SoftDeleteInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>()));

builder.Services.AddJobs(builder.Configuration);

builder.Services.AddDashboard();
builder.Services.AddCategorization(builder.Configuration);
builder.Services.AddImport();
builder.Services.AddBudgets(builder.Configuration);
builder.Services.AddTransactions();
builder.Services.AddSavings();
builder.Services.AddLimits();

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
    app.MapOpenApi();
    app.UseCors(devCors);

    using var scope = app.Services.CreateScope();
    var seedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedClock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

    if (app.Configuration.GetValue<bool>("Demo:Seed")) await DemoSeed.SeedAsync(seedDb, seedClock);
    else await DevSeed.SeedAsync(seedDb, seedClock);
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseJobsDashboard();
app.UseBudgetJobs();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/db", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { database = "up" })
        : Results.Problem("Brak połączenia z Postgresem — czy `docker compose up -d db` działa?"));

app.MapDashboard();
app.MapCategorization();
app.MapSavings();
app.MapLimits();
app.MapImport();
app.MapBudgets();
app.MapTransactions();

app.Run();

public partial class Program;
