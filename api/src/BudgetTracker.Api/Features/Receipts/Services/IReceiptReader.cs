using BudgetTracker.Api.Features.Receipts.Models;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Silnik OCR paragonów — jedyny punkt, w którym obraz paragonu wychodzi poza aplikację.</summary>
/// <remarks>
/// ⚠️ Implementacja wysyłająca plik do chmury jest świadomą decyzją (DECISIONS.md §15); dlatego wybór silnika
/// siedzi za interfejsem i w konfiguracji, a nie w handlerze.
/// </remarks>
public interface IReceiptReader
{
    /// <summary>Odczytuje sprzedawcę, datę i sumę. Rzuca <c>ReceiptReaderUnavailableException</c>, gdy odczyt się nie uda.</summary>
    Task<ReceiptReading> ReadAsync(BinaryData content, CancellationToken ct);
}
