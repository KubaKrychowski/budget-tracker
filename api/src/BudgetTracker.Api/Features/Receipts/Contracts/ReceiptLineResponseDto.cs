namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Produkt z paragonu — wiersz rozwijanej listy pod transakcją.</summary>
/// <param name="ProductCategoryId"><c>BusinessId</c> kategorii produktu; <c>null</c> = bez kategorii.</param>
/// <param name="CategoryConfirmed"><c>true</c>, gdy kategorię wybrał użytkownik; <c>false</c> = podpowiedź reguły albo modelu.</param>
public sealed record ReceiptLineResponseDto(
    string Name,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TotalPrice,
    Guid? ProductCategoryId,
    bool CategoryConfirmed);
