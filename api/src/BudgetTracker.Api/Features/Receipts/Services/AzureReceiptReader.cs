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

            var totalValue = total?.ValueCurrency is { } c ? Convert.ToDecimal(c.Amount, CultureInfo.InvariantCulture) : (decimal?)null;
            fields.TryGetValue("Items", out var items);

            return new ReceiptReading(
                merchant?.ValueString?.Trim(), merchant?.Confidence,
                date?.ValueDate is { } d ? DateOnly.FromDateTime(d.DateTime) : null, date?.Confidence,
                totalValue, ReceiptTotalCheck.Adjust(totalValue, total?.Confidence, ItemPrices(items)));
        }
        catch (Exception e) when (e is RequestFailedException or InvalidOperationException)
        {
            throw new ReceiptReaderUnavailableException(e);
        }
    }

    /// <summary>Ceny pozycji, ale tylko gdy MA je KAŻDA — suma części nie jest porównywalna z sumą całości.</summary>
    private static IReadOnlyList<decimal> ItemPrices(DocumentField? items)
    {
        if (items?.ValueList is not { Count: > 0 } list) return [];

        var prices = new List<decimal>();
        foreach (var item in list)
        {
            if (item.ValueDictionary is { } fields
                && fields.TryGetValue("TotalPrice", out var price)
                && price.ValueCurrency is { } amount)
            {
                prices.Add(Convert.ToDecimal(amount.Amount, CultureInfo.InvariantCulture));
            }
            else
            {
                return [];
            }
        }

        return prices;
    }
}
