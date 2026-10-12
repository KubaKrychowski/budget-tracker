using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;
using BudgetTracker.Api.Domain;
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
                totalValue, ReceiptTotalCheck.Adjust(totalValue, total?.Confidence, ItemPrices(items)),
                Lines(items));
        }
        catch (Exception e) when (e is RequestFailedException or InvalidOperationException)
        {
            throw new ReceiptReaderUnavailableException(e);
        }
    }

    /// <summary>Pozycje paragonu: nazwa z linii, ilość, cena jednostkowa i wartość; linie bez nazwy pomijamy.</summary>
    private static IReadOnlyList<ReceiptLine> Lines(DocumentField? items)
    {
        if (items?.ValueList is not { Count: > 0 } list) return [];

        var lines = new List<ReceiptLine>();
        foreach (var item in list)
        {
            if (item.ValueDictionary is not { } fields) continue;

            var name = fields.TryGetValue("Description", out var description) && description.ValueString is { } raw
                ? ReceiptLine.CleanName(raw)
                : null;
            if (string.IsNullOrEmpty(name)) continue;

            lines.Add(new ReceiptLine(
                name,
                fields.TryGetValue("Quantity", out var quantity) && quantity.ValueDouble is { } q
                    ? Convert.ToDecimal(q, CultureInfo.InvariantCulture) : null,
                Amount(fields, "Price"),
                Amount(fields, "TotalPrice")));
        }

        return lines;
    }

    private static decimal? Amount(IReadOnlyDictionary<string, DocumentField> fields, string key) =>
        fields.TryGetValue(key, out var field) && field.ValueCurrency is { } currency
            ? Convert.ToDecimal(currency.Amount, CultureInfo.InvariantCulture)
            : null;

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
