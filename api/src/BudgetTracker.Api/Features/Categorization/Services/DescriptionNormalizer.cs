using System.Text.RegularExpressions;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Sprowadza surowy opis z wyciągu do postaci, którą widzi <see cref="ICategorizer"/>.
/// </summary>
/// <remarks>
/// Kolejność kroków ma znaczenie i jest sprawdzona na realnym wyciągu PKO (1340 wierszy):
/// najpierw znikają etykiety i daty, potem numery, na końcu interpunkcja. Odwrócenie
/// kolejności zostawiłoby fragmenty numerów jako „słowa”.
///
/// Klasa jest czystą funkcją — bez interfejsu, bo CLAUDE.md §4 dopuszcza szew tylko tam,
/// gdzie realnie powstanie druga implementacja.
/// </remarks>
public static partial class DescriptionNormalizer
{
    /// <summary>Etykieta zagnieżdżona w wartości pola „Lokalizacja: Adres: …”.</summary>
    [GeneratedRegex(@"\badres:?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex NestedAddressLabel();

    /// <summary>„Kraj: POL” — nic nie wnosi do rozpoznania sprzedawcy.</summary>
    [GeneratedRegex(@"\bkraj:?\s*\w*", RegexOptions.IgnoreCase)]
    private static partial Regex CountryLabel();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\d{2}[./]\d{2}[./]\d{2,4}")]
    private static partial Regex LocalDate();

    /// <summary>Zamaskowane numery kart: „4111********1111”, „5555xxxx1234”.</summary>
    [GeneratedRegex(@"\b\d[\d*x]{5,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex MaskedCardNumber();

    /// <summary>Dłuższe ciągi cyfr — numery referencyjne, rachunki, identyfikatory terminali.</summary>
    [GeneratedRegex(@"\b\d{4,}\b")]
    private static partial Regex LongDigitRun();

    /// <summary>Wszystko poza literami, cyframi, spacją i `.&amp;-` (zostają, bo niosą sens w nazwach).</summary>
    [GeneratedRegex(@"[^\p{L}\p{N}\s.&-]")]
    private static partial Regex Punctuation();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Znormalizowany opis; pusty ciąg dla braku opisu.</summary>
    /// <remarks>
    /// <c>ToLowerInvariant</c>, nie <c>ToLower(CultureInfo)</c> — wynik nie może zależeć od ustawień
    /// regionalnych maszyny, bo ta sama transakcja musi dać ten sam opis wszędzie,
    /// a opis trafia do modelu i do klucza deduplikacji.
    /// </remarks>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var s = raw.ToLowerInvariant();

        s = NestedAddressLabel().Replace(s, " ");
        s = CountryLabel().Replace(s, " ");
        s = IsoDate().Replace(s, " ");
        s = LocalDate().Replace(s, " ");
        s = MaskedCardNumber().Replace(s, " ");
        s = LongDigitRun().Replace(s, " ");
        s = Punctuation().Replace(s, " ");
        s = Whitespace().Replace(s, " ");

        return s.Trim();
    }
}
