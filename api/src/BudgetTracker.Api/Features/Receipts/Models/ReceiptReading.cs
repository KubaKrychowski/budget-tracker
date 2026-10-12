namespace BudgetTracker.Api.Features.Receipts.Models;

/// <summary>Wynik odczytu paragonu — wartości mogą być puste, gdy OCR ich nie znalazł; pewność to liczba 0–1.</summary>
public sealed record ReceiptReading(
    string? Merchant,
    float? MerchantConfidence,
    DateOnly? Date,
    float? DateConfidence,
    decimal? Total,
    float? TotalConfidence);
