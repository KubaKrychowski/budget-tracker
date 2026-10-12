using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Models;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Czytnik na instalację bez skonfigurowanego OCR — każdy odczyt kończy się czytelnym 503.</summary>
/// <remarks>
/// ⚠️ Brak <c>Receipts:Endpoint</c> NIE przewraca startu API (inaczej niż <c>Storage:BlobServiceUri</c>): OCR jest dodatkiem,
/// a bez niego reszta aplikacji ma działać.
/// </remarks>
public sealed class UnconfiguredReceiptReader : IReceiptReader
{
    public Task<ReceiptReading> ReadAsync(BinaryData content, CancellationToken ct) =>
        throw new ReceiptReaderUnavailableException();
}
