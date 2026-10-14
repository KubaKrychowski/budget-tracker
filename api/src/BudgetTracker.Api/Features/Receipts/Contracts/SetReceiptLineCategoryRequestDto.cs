namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Wybór kategorii produktu dla jednej pozycji paragonu.</summary>
/// <param name="ProductCategoryId"><c>BusinessId</c> kategorii produktu; <c>null</c> zdejmuje kategorię.</param>
public sealed record SetReceiptLineCategoryRequestDto(Guid? ProductCategoryId);
