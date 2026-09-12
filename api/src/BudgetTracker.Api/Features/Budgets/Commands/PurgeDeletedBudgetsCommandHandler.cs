using BudgetTracker.Api.Features.Budgets.Services;
using Hangfire;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Fizycznie usuwa budżety skasowane dawniej niż <see cref="BudgetOptions.RetentionDays"/>, razem z dziećmi.
/// Uruchamiane cyklicznie przez Hangfire (zadanie <c>purge-deleted-budgets</c>, harmonogram <see cref="BudgetOptions.PurgeCron"/>).
/// </summary>
/// <remarks>
/// Soft delete bez sprzątania to nie „bezpieczne kasowanie", tylko baza, która nigdy nie maleje. Okno retencji jest po to,
/// żeby dało się cofnąć pomyłkę — po nim dane mają zniknąć naprawdę, bo tak brzmi obietnica z modala usuwania.
/// Samą mechanikę kasowania trzyma <see cref="BudgetPurger"/>; ten handler odpowiada tylko za PRÓG.
/// </remarks>
public sealed class PurgeDeletedBudgetsCommandHandler(
    BudgetPurger purger,
    IOptions<BudgetOptions> options,
    TimeProvider clock,
    ILogger<PurgeDeletedBudgetsCommandHandler> logger)
{
    /// <returns>Liczba usuniętych budżetów — Hangfire zapisuje ją jako wynik przebiegu w historii.</returns>
    /// <remarks>
    /// Idempotentne, więc ponowienie po błędzie jest bezpieczne. Liczba prób jest ograniczona do trzech, żeby historia
    /// zadań — która jest audytem — nie puchła od powtórzeń tego samego błędu (domyślnie Hangfire próbuje 10 razy).
    /// </remarks>
    [AutomaticRetry(Attempts = 3)]
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var threshold = clock.GetUtcNow() - TimeSpan.FromDays(options.Value.RetentionDays);

        var purged = await purger.PurgeAsync(threshold, ct);
        if (purged > 0) logger.LogInformation("Usunięto trwale {Count} budżetów.", purged);

        return purged;
    }
}
