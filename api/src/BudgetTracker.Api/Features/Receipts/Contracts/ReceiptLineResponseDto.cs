namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Produkt z paragonu — wiersz rozwijanej listy pod transakcją.</summary>
public sealed record ReceiptLineResponseDto(string Name, decimal? Quantity, decimal? UnitPrice, decimal? TotalPrice);
