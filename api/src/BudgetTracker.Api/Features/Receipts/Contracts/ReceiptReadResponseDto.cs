namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Odpowiedź po wgraniu paragonu: to, co odczytał OCR, do sprawdzenia przez użytkownika.</summary>
/// <param name="Id">Identyfikator zapisanego paragonu — adres kolejnych kroków (zapis, plik, usunięcie).</param>
/// <param name="Merchant">Sprzedawca; <c>null</c>, gdy OCR go nie odczytał.</param>
/// <param name="Date">Data zakupu; <c>null</c>, gdy OCR jej nie odczytał.</param>
/// <param name="Total">Suma, dodatnia; <c>null</c>, gdy OCR jej nie odczytał.</param>
/// <param name="MerchantUncertain">Pole puste albo pewność OCR poniżej progu — ekran oznacza je „sprawdź”.</param>
/// <param name="DateUncertain">Jak <paramref name="MerchantUncertain"/>, dla daty.</param>
/// <param name="TotalUncertain">Jak <paramref name="MerchantUncertain"/>, dla sumy.</param>
public sealed record ReceiptReadResponseDto(
    Guid Id,
    string FileName,
    string? Merchant,
    DateOnly? Date,
    decimal? Total,
    bool MerchantUncertain,
    bool DateUncertain,
    bool TotalUncertain);
