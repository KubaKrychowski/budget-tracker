using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Commands;

/// <summary>Usuwa paragon: wiersz miękko, a sam plik z magazynu naprawdę.</summary>
/// <remarks>
/// ⚠️ Plik kasujemy od razu, mimo soft delete wiersza: zdjęcie paragonu to dana osobowa i nie ma powodu trzymać go
/// po tym, jak użytkownik kazał je usunąć — wiersz zostaje tylko po to, żeby historia tabeli była spójna.
/// </remarks>
public sealed class DeleteReceiptCommandHandler(AppDbContext db, ReceiptFiles files)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.BusinessId == id, ct)
            ?? throw new ReceiptNotFoundException(id);

        await files.DeleteAsync(receipt.UserId, receipt.BlobName, ct);
        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(ct);
    }
}
