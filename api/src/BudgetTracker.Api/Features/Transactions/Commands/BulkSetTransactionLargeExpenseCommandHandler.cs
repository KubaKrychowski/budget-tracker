using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Commands;

/// <summary>Masowe ustawienie albo zdjęcie flagi „duży wydatek" z zasięgu zaznaczenia albo filtra.</summary>
public sealed class BulkSetTransactionLargeExpenseCommandHandler(
    AppDbContext db, TransactionSelectionResolver selection)
{
    public async Task<BulkActionResponseDto> HandleAsync(BulkSetLargeExpenseRequestDto request, CancellationToken ct)
    {
        var ids = await selection.ResolveAsync(request.Selection, ct);
        if (ids.Count == 0) return new BulkActionResponseDto(0);

        var affected = await db.Transactions
            .Where(t => ids.Contains(t.BusinessId))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsLargeExpense, request.IsLargeExpense), ct);

        return new BulkActionResponseDto(affected);
    }
}
