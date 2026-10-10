using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Models;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Odczyt paragonu gotowym modelem <c>prebuilt-receipt</c> z Azure Document Intelligence.</summary>
/// <remarks>
/// Uwierzytelnienie tożsamością zarządzaną API (rola „Cognitive Services User”), bez kluczy — jak magazyn blob.
/// ⚠️ Każde wywołanie to jedna strona płatna wg cennika usługi, więc nie wołamy modelu ponownie przy poprawianiu pól.
/// </remarks>
public sealed class AzureReceiptReader(DocumentIntelligenceClient client) : IReceiptReader
{
    private const string ModelId = "prebuilt-receipt";

    public async Task<ReceiptReading> ReadAsync(BinaryData content, CancellationToken ct)
    {
        try
        {
            var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, ModelId, content, cancellationToken: ct);
            var fields = operation.Value.Documents?.FirstOrDefault()?.Fields;
            if (fields is null) return new ReceiptReading(null, null, null, null, null, null);

            fields.TryGetValue("MerchantName", out var merchant);
            fields.TryGetValue("TransactionDate", out var date);
            fields.TryGetValue("Total", out var total);

            return new ReceiptReading(
                merchant?.ValueString?.Trim(), merchant?.Confidence,
                date?.ValueDate is { } d ? DateOnly.FromDateTime(d.DateTime) : null, date?.Confidence,
                total?.ValueCurrency is { } c ? Convert.ToDecimal(c.Amount, CultureInfo.InvariantCulture) : null, total?.Confidence);
        }
        catch (Exception e) when (e is RequestFailedException or InvalidOperationException)
        {
            throw new ReceiptReaderUnavailableException(e);
        }
    }
}
