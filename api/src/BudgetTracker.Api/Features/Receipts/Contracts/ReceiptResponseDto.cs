namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Paragon przypięty do transakcji — pozycja menu „Podgląd paragonu” i rozwijana lista produktów.</summary>
/// <param name="TransactionId">Transakcja, do której przypięto paragon; <c>null</c>, gdy paragon czeka bez przypięcia.</param>
/// <param name="Items">Produkty z paragonu; pusta lista, gdy OCR żadnego nie odczytał.</param>
public sealed record ReceiptResponseDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Merchant,
    DateOnly? Date,
    decimal? Total,
    Guid? TransactionId,
    IReadOnlyList<ReceiptLineResponseDto> Items);
