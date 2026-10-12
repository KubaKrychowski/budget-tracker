using BudgetTracker.Api.Domain;

namespace BudgetTracker.Api.Features.Receipts.Models;

/// <summary>Wynik odczytu paragonu — wartości mogą być puste, gdy OCR ich nie znalazł; pewność to liczba 0–1.</summary>
/// <param name="Items">Odczytane pozycje; <c>null</c> albo pusta = OCR żadnej nie znalazł.</param>
public sealed record ReceiptReading(
    string? Merchant,
    float? MerchantConfidence,
    DateOnly? Date,
    float? DateConfidence,
    decimal? Total,
    float? TotalConfidence,
    IReadOnlyList<ReceiptLine>? Items = null);
