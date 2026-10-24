using BudgetTracker.Api.Features.Categorization.Jobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Microsoft.AspNetCore.DataProtection;

namespace BudgetTracker.Api.Infrastructure.Jobs;

/// <summary>
/// Hangfire: zadania cykliczne w procesie API, storage w pamięci procesu.
/// </summary>
/// <remarks>
/// <para>
/// Zastępuje <c>BackgroundService</c>: procesy cykliczne mają jeden mechanizm harmonogramu, ponowień i historii,
/// a historia przebiegów w panelu <c>/hangfire</c> jest ich audytem (retencja z <see cref="JobsOptions.HistoryRetentionDays"/>).
/// Zadania rejestruje każdy feature u siebie (np. <c>BudgetsModule.UseBudgetJobs</c>).
/// </para>
/// <para>
/// ⚠️ Storage jest W PAMIĘCI, nie w Postgresie (REWIZJA 2026-10-24, DECISIONS.md). Serwer Hangfire na Postgresie
/// odpytuje kolejkę, harmonogram i heartbeat co 15–30 s, a przy Always On proces nie zasypia — darmowy Neon,
/// który usypia compute dopiero po 5 minutach ciszy, nie zasypiał nigdy i wyczerpywał limit compute.
/// Cena: restart procesu kasuje historię w panelu <c>/hangfire</c> i zadania czekające w kolejce (zadania
/// cykliczne rejestrują się od nowa przy starcie, więc harmonogram nie ginie). Działa tylko przy JEDNEJ
/// instancji aplikacji — dwie miałyby dwie niezależne kolejki.
/// </para>
/// </remarks>
public static class JobsModule
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JobsOptions>(config.GetSection(JobsOptions.SectionName));

        var jobs = config.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage(new InMemoryStorageOptions
            {
                // ⚠️ Identyfikatory GUID, nie kolejne liczby: numeracja w pamięci zaczyna się od 1 po każdym
                // restarcie, a JobId treningu jest zapisany w ModelVersion i po nim GetTrainingStatusQueryHandler
                // rozpoznaje „gotowe". Powtórzony numer pokazałby świeżo zgłoszony trening jako skończony
                // — z raportem starego przebiegu.
                IdType = InMemoryStorageIdType.Guid,

                // Domyślnie storage przycina wygasanie do 3 godzin, co po cichu skróciłoby historię ustawioną
                // przez JobHistoryRetentionFilter.
                MaxExpirationTime = TimeSpan.FromDays(jobs.HistoryRetentionDays),
            })
            .UseFilter(new JobHistoryRetentionFilter(TimeSpan.FromDays(jobs.HistoryRetentionDays))));

        services.AddDataProtection();
        services.AddSingleton<JobsDashboardAccess>();

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

    /// <summary>Panel Hangfire pod <c>/hangfire</c> oraz wejście dla administratora (<c>/hangfire/enter</c>).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>⚠️ <c>AllowAnonymous</c> jest KONIECZNE, a nie wygodne: aplikacja ma globalną politykę
    /// <c>RequireAuthenticatedUser</c>, a panel to zwykłe strony HTML, które nie mają jak wysłać tokenu
    /// dostępu. Autoryzację robi więc własny filtr panelu (<see cref="JobsDashboardAuthorizationFilter"/>).</item>
    /// <item>Na produkcji wchodzi się wyłącznie przez bilet z panelu administratora Identity i ciasteczko
    /// (<see cref="JobsDashboardAccess"/>). W Development panel dodatkowo odpowiada na połączenia z tej maszyny.</item>
    /// <item>⚠️ Panel działa w PEŁNYM trybie: pozwala uruchamiać, ponawiać i kasować zadania. Pokazuje też ARGUMENTY
    /// zadań, a w nich jedzie identyfikator użytkownika — dlatego dostęp ma wyłącznie administrator.</item>
    /// </list>
    /// </remarks>
    public static WebApplication UseJobsDashboard(this WebApplication app)
    {
        app.MapGet(JobsDashboardAccess.EnterPath, (string? ticket, HttpContext http, JobsDashboardAccess access) =>
            {
                if (!access.TryRedeemTicket(ticket)) return Results.Problem(statusCode: StatusCodes.Status403Forbidden);

                http.Response.Cookies.Append(JobsDashboardAccess.CookieName, access.IssueCookieValue(), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = JobsDashboardAccess.DashboardPath,
                    MaxAge = access.CookieLifetime,
                    IsEssential = true,
                });

                return Results.Redirect(JobsDashboardAccess.DashboardPath);
            })
            .AllowAnonymous();

        app.MapHangfireDashboard(JobsDashboardAccess.DashboardPath, new DashboardOptions
            {
                Authorization = [new JobsDashboardAuthorizationFilter(allowLocalRequests: app.Environment.IsDevelopment())],
                DashboardTitle = "Wydatki.com — zadania w tle",
            })
            .AllowAnonymous();

        return app;
    }
}
