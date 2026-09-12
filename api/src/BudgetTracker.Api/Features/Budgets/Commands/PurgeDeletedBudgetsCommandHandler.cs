using BudgetTracker.Api.Infrastructure;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Fizycznie usuwa budżety skasowane dawniej niż <see cref="BudgetOptions.RetentionDays"/>, razem z dziećmi.
/// Uruchamiane cyklicznie przez Hangfire (zadanie <c>purge-deleted-budgets</c>, harmonogram <see cref="BudgetOptions.PurgeCron"/>).
/// </summary>
/// <remarks>
/// <para>
/// Soft delete bez sprzątania to nie „bezpieczne kasowanie", tylko baza, która nigdy nie maleje. Okno retencji jest po to,
/// żeby dało się cofnąć pomyłkę — po nim dane mają zniknąć naprawdę, bo tak brzmi obietnica z modala usuwania.
/// </para>
/// <para>
/// ⚠️ To jedyne miejsce w aplikacji, w którym dane znikają fizycznie. <c>ExecuteDelete</c> omija <c>SoftDeleteInterceptor</c>
/// (ten działa na <c>SaveChanges</c>), a dzieci idą przed rodzicem, bo klucze obce są <c>Restrict</c> (CLAUDE.md §4).
/// </para>
/// </remarks>
public sealed class PurgeDeletedBudgetsCommandHandler(
    AppDbContext db,
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

        var expired = await db.Budgets
            .IgnoreQueryFilters()
            .Where(b => b.DeletedAt != null && b.DeletedAt <= threshold)
            .Select(b => new { b.Id, b.BusinessId })
            .ToListAsync(ct);

        if (expired.Count == 0) return 0;

        var expiredIds = expired.Select(b => b.Id).ToList();
        var expiredBusinessIds = expired.Select(b => b.BusinessId).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Transactions.IgnoreQueryFilters()
            .Where(t => t.BudgetBusinessId != null && expiredBusinessIds.Contains(t.BudgetBusinessId.Value))
            .ExecuteDeleteAsync(ct);

        await db.ImportBatches.IgnoreQueryFilters()
            .Where(i => expiredIds.Contains(i.BudgetId))
            .ExecuteDeleteAsync(ct);

        await db.BudgetItems.IgnoreQueryFilters()
            .Where(i => expiredBusinessIds.Contains(i.BudgetBusinessId))
            .ExecuteDeleteAsync(ct);

        await db.Budgets.IgnoreQueryFilters()
            .Where(b => expiredIds.Contains(b.Id))
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);

        logger.LogInformation("Usunięto trwale {Count} budżetów.", expired.Count);
        return expired.Count;
    }
}
