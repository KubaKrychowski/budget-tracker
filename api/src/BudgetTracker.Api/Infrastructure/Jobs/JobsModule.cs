using Hangfire;
using Hangfire.PostgreSql;

namespace BudgetTracker.Api.Infrastructure.Jobs;

/// <summary>
/// Hangfire: zadania cykliczne w procesie API, storage w tym samym Postgresie (schemat <c>hangfire</c>).
/// </summary>
/// <remarks>
/// <para>
/// Zastępuje <c>BackgroundService</c>: procesy cykliczne mają jeden mechanizm harmonogramu, ponowień i historii,
/// a historia przebiegów w panelu <c>/hangfire</c> jest ich audytem (retencja z <see cref="JobsOptions.HistoryRetentionDays"/>).
/// Zadania rejestruje każdy feature u siebie (np. <c>BudgetsModule.UseBudgetJobs</c>).
/// </para>
/// <para>
/// ⚠️ Schemat <c>hangfire</c> zakłada Hangfire sam przy starcie — jest poza migracjami EF, a <c>docker compose down -v</c>
/// kasuje go razem z historią.
/// </para>
/// </remarks>
public static class JobsModule
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JobsOptions>(config.GetSection(JobsOptions.SectionName));

        var jobs = config.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres' — Hangfire nie ma gdzie trzymać zadań.");

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true })
            .UseFilter(new JobHistoryRetentionFilter(TimeSpan.FromDays(jobs.HistoryRetentionDays))));

        services.AddHangfireServer();
        return services;
    }

    /// <summary>Panel Hangfire pod <c>/hangfire</c> — wyłącznie w środowisku deweloperskim.</summary>
    public static WebApplication UseJobsDashboard(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseHangfireDashboard("/hangfire");
        }

        return app;
    }
}
