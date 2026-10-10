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
            20.60m, 0.62f));
}
