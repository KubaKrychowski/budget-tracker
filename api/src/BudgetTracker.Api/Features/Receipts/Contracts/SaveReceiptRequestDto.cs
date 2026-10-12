namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Zapis ekranu weryfikacji: poprawione pola i to, co zrobić z transakcją.</summary>
/// <param name="TransactionId">Transakcja do przypięcia; <c>null</c> = paragon nie jest przypinany do istniejącej.</param>
/// <param name="CreateTransaction">
/// Przy <c>TransactionId = null</c>: <c>true</c> zakłada NOWĄ transakcję z paragonu (data, sprzedawca, kwota ujemna),
/// <c>false</c> zostawia paragon bez przypięcia. Przy podanym <c>TransactionId</c> ignorowane.
/// </param>
/// <param name="BudgetId">Budżet nowej transakcji; <c>null</c> = budżet domyślny, jak na liście transakcji.</param>
public sealed record SaveReceiptRequestDto(
    Guid? TransactionId,
    string? Merchant,
    DateOnly? Date,
    decimal? Total,
    bool CreateTransaction = false,
    Guid? BudgetId = null);
