namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Paragon przypięty do transakcji — pozycja menu „Podgląd paragonu”.</summary>
public sealed record ReceiptResponseDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Merchant,
    DateOnly? Date,
    decimal? Total);
