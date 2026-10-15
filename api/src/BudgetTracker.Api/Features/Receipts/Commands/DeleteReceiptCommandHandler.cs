using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Commands;

/// <summary>Usuwa paragon: plik z magazynu i wiersz z bazy, naprawdę, od razu.</summary>
/// <remarks>
/// <para>
/// ⚠️ Kasowanie jest FIZYCZNE, nie miękkie (<c>ExecuteDeleteAsync</c> omija <c>SoftDeleteInterceptor</c>). Wiersz paragonu niesie sprzedawcę,
/// datę, sumę, nazwy kupionych produktów i oryginalną nazwę pliku, a nic w aplikacji nie sprząta miękko skasowanych paragonów
/// (retencja dotyczy tylko budżetów) — wiersz po „usunięciu” zostawałby w bazie na stałe, mimo że polityka prywatności obiecuje
/// usunięcie paragonu od razu.
/// </para>
/// <para>Plik idzie pierwszy: gdy kasowanie z magazynu się nie powiedzie, wiersz zostaje i użytkownik może ponowić usunięcie.</para>
/// </remarks>
public sealed class DeleteReceiptCommandHandler(AppDbContext db, ReceiptFiles files)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.BusinessId == id, ct)
            ?? throw new ReceiptNotFoundException(id);

        await files.DeleteAsync(receipt.UserId, receipt.BlobName, ct);
        await db.Receipts.Where(r => r.BusinessId == id).ExecuteDeleteAsync(ct);
    }
}
