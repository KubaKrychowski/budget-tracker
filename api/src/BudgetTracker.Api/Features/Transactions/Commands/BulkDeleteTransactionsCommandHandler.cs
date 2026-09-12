using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Commands;

/// <summary>Masowe usunięcie transakcji z zasięgu zaznaczenia albo filtra.</summary>
/// <remarks>
/// Kasowanie logiczne przez jawny stempel <c>DeletedAt</c> w <c>ExecuteUpdate</c> — ta ścieżka omija
/// <c>SoftDeleteInterceptor</c>, więc <c>ExecuteDelete</c> skasowałby wiersze fizycznie.
/// </remarks>
public sealed class BulkDeleteTransactionsCommandHandler(
    AppDbContext db, TransactionSelectionResolver selection, TimeProvider clock)
{
    public async Task<BulkActionResponseDto> HandleAsync(BulkDeleteRequestDto request, CancellationToken ct)
    {
        var ids = await selection.ResolveAsync(request.Selection, ct);
        if (ids.Count == 0) return new BulkActionResponseDto(0);

        var now = clock.GetUtcNow();
        var affected = await db.Transactions
            .Where(t => ids.Contains(t.BusinessId))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedAt, now), ct);

        return new BulkActionResponseDto(affected);
    }
}
