using System.Text.RegularExpressions;

namespace BudgetTracker.Api.Features.ProductCategories.Services;

/// <summary>Sprowadza nazwę produktu z paragonu do postaci, którą widzą reguły i model: małe litery, bez liczb i jednostek.</summary>
/// <remarks>
/// <para>
/// ⚠️ Gramatury, objętości i procenty znikają celowo: „Mleko UHT 3,2% 1 l” i „Mleko UHT 2% 500 ml” to ten sam produkt, a reguła nauczona
/// z pierwszego ma złapać drugi. Zostaje to, co odróżnia produkty słowami.
/// </para>
/// <para>Czysta funkcja — jak <c>DescriptionNormalizer</c>, ale osobna, bo opis z wyciągu i nazwa z paragonu mają inny szum.</para>
/// </remarks>
public static partial class ProductNameNormalizer
{
    /// <summary>Liczba z opcjonalną jednostką doklejoną albo po spacji („320 g”, „0,5l”, „3,2%”, „6 szt”).</summary>
    [GeneratedRegex(@"\d+(?:[.,]\d+)?\s*(?:kg|dag|mg|g|ml|cl|l|szt|opak|x|%)?(?!\p{L})", RegexOptions.IgnoreCase)]
    private static partial Regex NumberWithUnit();

    [GeneratedRegex(@"[^\p{L}\s]")]
    private static partial Regex NonLetters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Znormalizowana nazwa; pusty ciąg dla braku nazwy.</summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var s = NumberWithUnit().Replace(raw.ToLowerInvariant(), " ");
        s = NonLetters().Replace(s, " ");
        return Whitespace().Replace(s, " ").Trim();
    }
}
