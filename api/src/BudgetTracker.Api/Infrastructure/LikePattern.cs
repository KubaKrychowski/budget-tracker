namespace BudgetTracker.Api.Infrastructure;

/// <summary>Tekst od użytkownika jako wzorzec <c>ILIKE</c> „zawiera” — wspólne dla wyszukiwarki, reguł zleceń i kandydatów.</summary>
public static class LikePattern
{
    /// <summary>Znak ucieczki dla <c>ILIKE</c> — musi być podany jawnie, Postgres nie zakłada żadnego.</summary>
    public const string EscapeChar = "\\";

    /// <summary>Wzorzec „zawiera <paramref name="value"/>” z zneutralizowanymi metaznakami.</summary>
    /// <remarks>
    /// Bez ucieczki szukanie „5%" zamienia się we wzorzec <c>%5%%</c>, czyli po prostu „zawiera 5" — i zwraca
    /// „ODSETKI 15 PLN" jako trafienie. To nie jest dziura na wstrzyknięcie SQL (zapytanie leci parametrem), tylko
    /// cicho błędne wyniki. Backslash pierwszy, inaczej podwoiłby ucieczki dopisane w kolejnych krokach.
    /// </remarks>
    public static string Contains(string value) => $"%{Escape(value)}%";

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
}
