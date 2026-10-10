using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Receipts.Commands;

/// <summary>Przyjmuje plik paragonu, odczytuje go OCR-em i zapisuje wynik — do sprawdzenia przez użytkownika.</summary>
/// <remarks>
/// ⚠️ Kolejność: najpierw odczyt, potem zapis pliku. Gdy usługa OCR jest niedostępna, nie zostaje po tym
/// osierocony blob ani wiersz bez pól.
/// </remarks>
public sealed class UploadReceiptCommandHandler(
    AppDbContext db,
    IReceiptReader reader,
    ReceiptFiles files,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    /// <summary>Pewność OCR poniżej tej wartości oznacza pole „sprawdź”.</summary>
    public const float ConfidenceThreshold = 0.8f;

    public async Task<ReceiptReadResponseDto> HandleAsync(
        Stream content, string fileName, string contentType, long length, CancellationToken ct)
    {
        if (length is <= 0 or > ReceiptFiles.MaxSizeBytes || !ReceiptFiles.IsSupported(contentType))
        {
            throw new ReceiptFileInvalidException();
        }

        var data = await BinaryData.FromStreamAsync(content, ct);
        var reading = await reader.ReadAsync(data, ct);

        var userId = currentUser.UserId;
        var blobName = ReceiptFiles.NewBlobName(contentType);
        await files.SaveAsync(userId, blobName, data, contentType, ct);

        var total = reading.Total is > 0 ? reading.Total : null;
        var receipt = new Receipt(
            userId, blobName, Path.GetFileName(fileName), contentType, length, clock.GetUtcNow(),
            reading.Merchant, reading.Date, total);

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        return new ReceiptReadResponseDto(
            receipt.BusinessId, receipt.FileName, receipt.Merchant, receipt.ReceiptDate, receipt.Total,
            Uncertain(receipt.Merchant, reading.MerchantConfidence),
            Uncertain(receipt.ReceiptDate, reading.DateConfidence),
            Uncertain(receipt.Total, reading.TotalConfidence));
    }

    private static bool Uncertain<T>(T? value, float? confidence) => value is null || confidence is null or < ConfidenceThreshold;
}
