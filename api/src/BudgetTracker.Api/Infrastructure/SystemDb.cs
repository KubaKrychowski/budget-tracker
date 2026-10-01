using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Kontekst bazy dla zadań SYSTEMOWYCH (sprzątanie budżetów, operacje administracyjne na danych właściciela, seedy) —
/// łączy się OSOBNĄ rolą i OSOBNYM connection stringiem, a nie zwykłą rolą aplikacji.
/// </summary>
/// <remarks>
/// <para>
/// Po co: dotąd zadania systemowe wchodziły w <c>budget_jobs</c> (<c>BYPASSRLS</c>) przez <c>SET LOCAL ROLE</c>, więc
/// aplikacja jako <c>budget_app</c> MIAŁA prawo stać się rolą omijającą RLS. Każdy, kto wszedł w posiadanie
/// connection stringa <c>budget_app</c> (log, kopia ustawień, laptop), mógł wykonać to samo i przeczytać dane
/// wszystkich użytkowników — izolacja chroniła przed błędem w kodzie, ale nie przed wyciekiem poświadczeń.
/// Teraz ścieżka obsługująca żądania webowe używa wyłącznie <c>budget_app</c> (bez prawa do przełączenia roli),
/// a rola omijająca RLS (<c>budget_worker</c>) ma własny connection string, którego żądania nie używają.
/// </para>
/// <para>
/// ⚠️ Zakres: oba connection stringi nadal leżą w ustawieniach tej samej aplikacji, więc włamanie DO APLIKACJI
/// daje oba. Ta zmiana chroni przed wyciekiem SAMEGO stringa webowego, nie przed przejęciem procesu.
/// </para>
/// <para>
/// Przejście przejściowe: gdy <c>ConnectionStrings:PostgresWorker</c> nie jest ustawiony, kontekst systemowy używa
/// zwykłego stringa, a <see cref="EnterSystemRoleAsync"/> robi stary <c>SET LOCAL ROLE budget_jobs</c>. Dzięki temu
/// lokalny dev, CI i wdrożenie ZANIM powstanie rola <c>budget_worker</c> działają bez zmian. Po wdrożeniu roli i
/// odebraniu <c>budget_app</c> członkostwa w <c>budget_jobs</c> (api/db/separate-worker-role.sql) ten zapasowy
/// tor przestaje działać — i o to chodzi.
/// </para>
/// </remarks>
public static class SystemDb
{
    /// <summary>Klucz w kontenerze DI: <c>[FromKeyedServices(SystemDb.Key)] AppDbContext</c>.</summary>
    public const string Key = "system";

    public const string WorkerConnectionName = "PostgresWorker";

    public static IServiceCollection AddSystemDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedScoped<AppDbContext>(Key, (sp, _) =>
        {
            // ⚠️ IsNullOrWhiteSpace, nie ??: Terraform ustawia PostgresWorker na pusty string, gdy roli jeszcze nie ma.
            var connection = configuration.HasWorkerConnection()
                ? configuration.GetConnectionString(WorkerConnectionName)
                : configuration.GetConnectionString("Postgres");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection)
                .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>())
                .Options;

            // Bez ICurrentUserAccessor i bez interceptorów RLS: to ścieżka bez kontekstu użytkownika.
            return new AppDbContext(options);
        });

        return services;
    }

    /// <summary>Czy skonfigurowano osobny connection string roli systemowej.</summary>
    public static bool HasWorkerConnection(this IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString(WorkerConnectionName));

    /// <summary>
    /// Przygotowuje bieżącą TRANSAKCJĘ do pracy bez RLS. Gdy połączenie jest już rolą omijającą RLS
    /// (<c>budget_worker</c> albo superuser w testach), nic nie robi; w przeciwnym razie — tor zapasowy opisany w
    /// uwagach do klasy: <c>SET LOCAL ROLE budget_jobs</c>.
    /// </summary>
    public static async Task EnterSystemRoleAsync(this AppDbContext db, CancellationToken ct = default)
    {
        var alreadyBypasses = await db.Database
            .SqlQueryRaw<bool>("SELECT rolbypassrls OR rolsuper AS \"Value\" FROM pg_roles WHERE rolname = current_user")
            .SingleAsync(ct);

        if (alreadyBypasses) return;

        await db.Database.ExecuteSqlRawAsync("SET LOCAL ROLE budget_jobs", ct);
    }
}
