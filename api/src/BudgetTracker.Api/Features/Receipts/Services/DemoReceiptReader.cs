using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Receipts.Models;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Czytnik demo: zwraca zmyślone pola bez wysyłania pliku gdziekolwiek.</summary>
/// <remarks>Używany tylko przy <c>Demo:Seed=true</c> i braku <c>Receipts:Endpoint</c> — do zrzutów i przeglądania UI.</remarks>
public sealed class DemoReceiptReader(TimeProvider clock) : IReceiptReader
{
    public Task<ReceiptReading> ReadAsync(BinaryData content, CancellationToken ct) =>
        Task.FromResult(new ReceiptReading(
            "Sklep Przykład", 0.97f,
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-1), 0.95f,
            20.60m, 0.62f,
            [
                new ReceiptLine("Chleb pszenny", 1m, 4.50m, 4.50m),
                new ReceiptLine("Mleko 3,2%", 1m, 3.20m, 3.20m),
                new ReceiptLine("Ser żółty", 1m, 12.90m, 12.90m),
            ]));
}
