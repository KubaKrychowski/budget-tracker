namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Zapis ekranu weryfikacji: poprawione pola i transakcja, do której należy paragon.</summary>
/// <param name="TransactionId">Transakcja do przypięcia; <c>null</c> = zapisz paragon bez przypinania.</param>
public sealed record SaveReceiptRequestDto(Guid? TransactionId, string? Merchant, DateOnly? Date, decimal? Total);
