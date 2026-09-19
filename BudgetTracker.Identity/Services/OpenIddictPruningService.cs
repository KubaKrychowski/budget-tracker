using BudgetTracker.Identity.Options;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace BudgetTracker.Identity.Services;

/// <summary>
/// Okresowo usuwa z bazy wygasłe tokeny i osierocone autoryzacje OpenIddict. Bez tego tabele rosną w nieskończoność:
/// każde odnowienie sesji (silent renew) dokłada wiersze, a nic ich nie kasuje.
/// </summary>
/// <remarks>
/// Kasujemy dopiero rekordy starsze niż <see cref="TokenLifetimeOptions.PruneAfter"/>, nie wszystkie wygasłe: świeżo
/// wygasły token bywa jeszcze potrzebny do rozpoznania ponownego użycia kodu albo refresh tokena. Tokeny idą przed
/// autoryzacjami, bo autoryzacja bez tokenów jest właśnie tym, co sprząta drugi krok.
/// </remarks>
public sealed class OpenIddictPruningService(
    IServiceScopeFactory scopeFactory,
    IOptions<TokenLifetimeOptions> options,
    TimeProvider clock,
    ILogger<OpenIddictPruningService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PruneInterval, clock);

        // Pierwszy przebieg od razu przy starcie: serwer wstaje rzadko, a zaległości są dziś (100 tokenów w bazie dev).
        do
        {
            await PruneAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    internal async Task PruneAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var threshold = clock.GetUtcNow() - options.Value.PruneAfter;

            await scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>().PruneAsync(threshold, ct);
            await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>().PruneAsync(threshold, ct);

            logger.LogInformation("Wyczyszczono tokeny i autoryzacje OpenIddict starsze niż {Threshold:u}.", threshold);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // zatrzymanie serwera w trakcie czyszczenia — nic do zrobienia
        }
        catch (Exception ex)
        {
            // Sprzątanie nie może położyć serwera logowania; kolejna próba za PruneInterval.
            logger.LogError(ex, "Czyszczenie tokenów OpenIddict nie powiodło się.");
        }
    }
}
