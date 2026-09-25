using BudgetTracker.Api.Features.Categorization.Jobs;
using Hangfire;
using Hangfire.Dashboard;
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

        // ⚠️ Trening chodzi na WŁASNEJ kolejce z JEDNYM wykonawcą. Domyślny serwer ma 20 workerów,
        // więc samo wrzucenie zadania do Hangfire'a niczego nie szereguje — dwa treningi poszłyby
        // równolegle i biły się o procesor z importem. Serwer domyślny nasłuchuje tylko „default”,
        // więc nie podbierze zadań z tej kolejki.
        //
        // ⚠️ To szereguje w obrębie PROCESU. Przy dwóch instancjach aplikacji każda postawi własny
        // serwer z jednym wykonawcą i równoległość wróci — wtedy potrzebna jest blokada rozproszona
        // albo niezmiennik w bazie (jedna aktywna wersja na użytkownika).
        services.AddHangfireServer(options =>
        {
            options.ServerName = $"{Environment.MachineName}:training";
            options.Queues = [TrainCategoryModelJob.QueueName];
            options.WorkerCount = 1;
        });

        return services;
    }

    /// <summary>Panel Hangfire pod <c>/hangfire</c> — wyłącznie w środowisku deweloperskim.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>⚠️ <c>AllowAnonymous</c> jest KONIECZNE, a nie wygodne: aplikacja ma globalną politykę
    /// <c>RequireAuthenticatedUser</c>, a panel to zwykłe strony HTML, które nie mają jak wysłać tokenu
    /// dostępu. Bez tego każde wejście kończyło się 401 i pustym ekranem zamiast panelu.</item>
    /// <item>Dostęp ogranicza <see cref="LocalRequestsOnlyAuthorizationFilter"/> — panel odpowiada tylko
    /// na połączenia z tej maszyny. To jedyna ochrona, jaką ma, więc mapujemy go wyłącznie
    /// w Development (CLAUDE.md §4).</item>
    /// <item>⚠️ Panel pokazuje ARGUMENTY zadań, a w nich jedzie identyfikator użytkownika. To kolejny
    /// powód, żeby nie wystawiać go poza maszyną deweloperską. Wersja produkcyjna wymagałaby własnego
    /// filtru sprawdzającego rolę administratora (<c>AdminModule.ConfigureAdminPolicy</c>).</item>
    /// </list>
    /// </remarks>
    public static WebApplication UseJobsDashboard(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHangfireDashboard("/hangfire", new DashboardOptions
                {
                    Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
                })
                .AllowAnonymous();
        }

        return app;
    }
}
