using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Queries;

/// <summary>Otwiera plik paragonu do podglądu albo pobrania.</summary>
public sealed class GetReceiptFileQueryHandler(AppDbContext db, ReceiptFiles files)
{
    public async Task<(Stream Content, string ContentType, string FileName)> HandleAsync(Guid id, CancellationToken ct)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.BusinessId == id, ct)
            ?? throw new ReceiptNotFoundException(id);

        return (await files.OpenAsync(receipt.UserId, receipt.BlobName, ct), receipt.ContentType, receipt.FileName);
    }
}
