using Azure.Core;
using Npgsql;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>Rozgrzewka po starcie procesu: pierwszy token tożsamości zarządzanej i pierwsze połączenie z bazą.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ Bez tego pierwsze żądanie po KAŻDYM starcie (wdrożenie, restart po zmianie ustawień, nowa instancja) płaci za
/// pierwszy token z lokalnego endpointu App Service (ok. 5 s w telemetrii) i za zimne połączenie z Neonem
/// (ok. 1 s, a po uśpieniu bazy kilka sekund).</item>
/// <item>Rozgrzewa TEN SAM obiekt <see cref="TokenCredential"/>, którego używa klient bloba — jest singletonem
/// z <c>Program.cs</c>. Rozgrzanie osobnej instancji nic by nie dało, bo pamięć podręczna tokenu jest per obiekt.</item>
/// <item>Działa w tle i nie blokuje startu. Błąd to tylko ostrzeżenie po kilku próbach: rozgrzewka jest optymalizacją,
/// nie warunkiem działania aplikacji.</item>
/// <item>Połączenie otwiera <see cref="NpgsqlConnection"/> na tym samym connection stringu co <c>AppDbContext</c>,
/// więc zasila tę samą pulę. Nie używa <c>AppDbContext</c>, bo jego interceptory RLS wymagają kontekstu użytkownika.</item>
/// <item>Rejestrowana tylko poza Development (tam tożsamość zarządzana jest wyłączona).</item>
/// </list>
/// </remarks>
public sealed class AzureWarmupService(
    TokenCredential credential,
    IConfiguration configuration,
    ILogger<AzureWarmupService> logger) : BackgroundService
{
    private static readonly string[] StorageScopes = ["https://storage.azure.com/.default"];
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(WarmTokenAsync(stoppingToken), WarmDatabaseAsync(stoppingToken));
    }

    private async Task WarmTokenAsync(CancellationToken ct)
    {
        await RetryAsync("token tożsamości", async () =>
            await credential.GetTokenAsync(new TokenRequestContext(StorageScopes), ct), ct);
    }

    private async Task WarmDatabaseAsync(CancellationToken ct)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await RetryAsync("połączenie z bazą", async () =>
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(ct);
        }, ct);
    }

    private async Task RetryAsync(string what, Func<Task> operation, CancellationToken ct)
    {
        var started = TimeProvider.System.GetTimestamp();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await operation();
                logger.LogInformation(
                    "Rozgrzewka ({What}): gotowe po {Elapsed:F1} s, próba {Attempt}.",
                    what, TimeProvider.System.GetElapsedTime(started).TotalSeconds, attempt + 1);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                if (attempt >= RetryDelays.Length)
                {
                    logger.LogWarning(e, "Rozgrzewka ({What}): poddano się po {Attempts} próbach.", what, attempt + 1);
                    return;
                }

                logger.LogInformation(e, "Rozgrzewka ({What}): próba {Attempt} nieudana, ponawiam.", what, attempt + 1);
                try { await Task.Delay(RetryDelays[attempt], ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
