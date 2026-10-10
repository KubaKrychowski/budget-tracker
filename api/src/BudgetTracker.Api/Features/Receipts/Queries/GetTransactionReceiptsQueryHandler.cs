using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Transactions.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Queries;

/// <summary>Paragony przypięte do transakcji, od najnowszego.</summary>
public sealed class GetTransactionReceiptsQueryHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<ReceiptResponseDto>> HandleAsync(Guid transactionId, CancellationToken ct)
    {
        if (!await db.Transactions.AnyAsync(t => t.BusinessId == transactionId, ct))
        {
            throw new TransactionNotFoundException(transactionId);
        }

        return await db.Receipts
            .Where(r => r.TransactionBusinessId == transactionId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReceiptResponseDto(
                r.BusinessId, r.FileName, r.ContentType, r.SizeBytes, r.Merchant, r.ReceiptDate, r.Total))
            .ToListAsync(ct);
    }
}
