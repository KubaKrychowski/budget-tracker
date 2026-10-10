using System.Text.RegularExpressions;

namespace BudgetTracker.ErrorReporting;

/// <summary>
/// Zamazuje dane wrażliwe, zanim tekst trafi do issue na GitHubie.
/// </summary>
/// <remarks>
/// ⚠️ To siatka bezpieczeństwa, nie gwarancja: issue ląduje w zewnętrznym serwisie i zostaje tam na zawsze
/// (także po usunięciu). Dlatego do issue nie trafiają nagłówki, ciała żądań ani query string — maskowanie
/// łapie tylko to, co wycieknie przez komunikat wyjątku albo linię logu.
/// </remarks>
public static partial class ErrorMasker
{
    private const string Mask = "***";

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]*")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?i)\b(Bearer|Basic)\s+[A-Za-z0-9._~+/\-=]+")]
    private static partial Regex AuthScheme();

    [GeneratedRegex(@"(?i)\b(password|pwd|secret|token|api[_-]?key|client[_-]?secret|access[_-]?key|accountkey)\b(\s*[=:]\s*)(""[^""]*""|'[^']*'|[^\s;,&]+)")]
    private static partial Regex KeyValueSecret();

    [GeneratedRegex(@"(?<![\w.])(?:\d[ -]?){12,}\d(?![\w.])")]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){3,7}(?: ?[A-Z0-9]{1,4})?\b")]
    private static partial Regex Iban();

    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        text = Jwt().Replace(text, Mask);
        text = AuthScheme().Replace(text, m => $"{m.Groups[1].Value} {Mask}");
        text = KeyValueSecret().Replace(text, m => $"{m.Groups[1].Value}{m.Groups[2].Value}{Mask}");
        text = Email().Replace(text, Mask);
        text = Iban().Replace(text, Mask);
        text = LongNumber().Replace(text, Mask);
        return text;
    }
}
