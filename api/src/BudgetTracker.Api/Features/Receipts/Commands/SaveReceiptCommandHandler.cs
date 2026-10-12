using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Commands;

/// <summary>Zapisuje poprawione pola paragonu i przypina go do wskazanej transakcji (albo zostawia bez przypięcia).</summary>
public sealed class SaveReceiptCommandHandler(AppDbContext db)
{
    public const int MaxMerchantLength = 200;

    public async Task<ReceiptResponseDto> HandleAsync(Guid id, SaveReceiptRequestDto request, CancellationToken ct)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.BusinessId == id, ct)
            ?? throw new ReceiptNotFoundException(id);

        var merchant = string.IsNullOrWhiteSpace(request.Merchant) ? null : request.Merchant.Trim();
        if (merchant?.Length > MaxMerchantLength || request.Total is <= 0)
        {
            throw new ReceiptFieldsInvalidException();
        }

        if (request.TransactionId is { } transactionId
            && !await db.Transactions.AnyAsync(t => t.BusinessId == transactionId, ct))
        {
            throw new ReceiptTransactionInvalidException();
        }

        receipt.Correct(merchant, request.Date, request.Total);
        receipt.AttachTo(request.TransactionId);
        await db.SaveChangesAsync(ct);

        return new ReceiptResponseDto(
            receipt.BusinessId, receipt.FileName, receipt.ContentType, receipt.SizeBytes,
            receipt.Merchant, receipt.ReceiptDate, receipt.Total);
    }
}
