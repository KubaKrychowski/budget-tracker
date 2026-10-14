using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Receipts.Contracts;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Zamiana encji paragonu na kontrakty HTTP — jedno miejsce, bo kilka handlerów zwraca ten sam kształt.</summary>
public static class ReceiptMapping
{
    public static IReadOnlyList<ReceiptLineResponseDto> Lines(IEnumerable<ReceiptLine> items) =>
        [.. items.Select(i => new ReceiptLineResponseDto(i.Name, i.Quantity, i.UnitPrice, i.TotalPrice, i.ProductCategoryId, i.CategoryConfirmed))];

    public static ReceiptResponseDto ToResponse(Receipt receipt) => new(
        receipt.BusinessId, receipt.FileName, receipt.ContentType, receipt.SizeBytes,
        receipt.Merchant, receipt.ReceiptDate, receipt.Total, receipt.TransactionBusinessId, Lines(receipt.Items));
}
