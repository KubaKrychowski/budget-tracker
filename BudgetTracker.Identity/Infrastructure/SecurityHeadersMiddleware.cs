using BudgetTracker.Identity.Services;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Nagłówki bezpieczeństwa dla wszystkich odpowiedzi Identity. Najważniejszy jest CSP: ekrany logowania są
/// jedynym miejscem, gdzie użytkownik wpisuje hasło i kod 2FA, więc wstrzyknięty skrypt = przejęte konto.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, SpaOrigins spaOrigins)
{
    /// <summary>
    /// Jedyna ścieżka, którą wolno osadzić w ramce — punkt autoryzacji, bo tamtędy idzie ciche odnowienie sesji.
    /// </summary>
    /// <remarks>
    /// ⚠️ Odnawianie sesji w SPA to UKRYTA RAMKA na <c>/connect/authorize?prompt=none</c>. Przy
    /// <c>frame-ancestors 'none'</c> i <c>X-Frame-Options: DENY</c> przeglądarka blokuje ją bez śladu
    /// w logach serwera, a jedynym objawem jest 401 po wygaśnięciu tokenu (15 minut). Tak było do 2026-09-26:
    /// ciche odnawianie nie działało nigdzie, także lokalnie.
    ///
    /// ⚠️ Poluzowanie dotyczy WYŁĄCZNIE tej ścieżki. Ekrany logowania, 2FA i usuwania konta zostają przy
    /// <c>DENY</c> — to na nich wpisuje się hasło, więc osadzenie ich w cudzej ramce jest dokładnie tym
    /// atakiem, przed którym ten nagłówek chroni. Przy <c>prompt=none</c> punkt autoryzacji nie renderuje
    /// zresztą żadnego formularza: albo przekierowuje z kodem, albo z błędem.
    /// </remarks>
    private const string FrameablePath = "/connect/authorize";

    private readonly string defaultPolicy = BuildContentSecurityPolicy(spaOrigins.All);
    private readonly string frameablePolicy = BuildContentSecurityPolicy(spaOrigins.All, frameableBy: spaOrigins.All);

    public Task InvokeAsync(HttpContext context)
    {
        var frameable = context.Request.Path.StartsWithSegments(FrameablePath, StringComparison.OrdinalIgnoreCase);

        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = frameable ? frameablePolicy : defaultPolicy;
        headers.XContentTypeOptions = "nosniff";

        // ⚠️ X-Frame-Options nie zna listy originów — ma wyłącznie DENY albo SAMEORIGIN. Na ścieżce
        // osadzalnej pomijamy go całkiem i zdajemy się na `frame-ancestors`, które origin rozróżnia.
        // Zostawienie DENY obok CSP wygrywałoby w części przeglądarek i ramka dalej byłaby blokowana.
        if (!frameable)
        {
            headers.XFrameOptions = "DENY";
        }

        // Kod autoryzacyjny jedzie w adresie; bez tego nagłówka mógłby wyciec w Referer do zewnętrznego zasobu.
        headers["Referrer-Policy"] = "no-referrer";

        return next(context);
    }

    /// <summary>
    /// <c>script-src 'self'</c> bez <c>unsafe-inline</c>: widoki nie mają skryptów ani stylów inline (patrz
    /// <c>auth.css</c>). Czcionki idą z Google Fonts, reszta tylko z własnego originu.
    /// </summary>
    /// <param name="frameableBy">
    /// Originy, którym wolno osadzić odpowiedź w ramce. Pusta lista = <c>frame-ancestors 'none'</c>.
    /// </param>
    /// <remarks>
    /// ⚠️ <c>form-action</c> zawiera originy SPA, bo Chrome sprawdza je także dla PRZEKIEROWAŃ po wysłaniu formularza:
    /// logowanie i zgoda kończą się przekierowaniem na <c>redirect_uri</c> frontu, więc bez tego przeglądarka
    /// zablokowałaby powrót do aplikacji.
    /// </remarks>
    public static string BuildContentSecurityPolicy(
        IEnumerable<string> spaOrigins, IEnumerable<string>? frameableBy = null)
    {
        var ancestors = frameableBy is null ? [] : frameableBy.ToArray();

        return string.Join("; ",
        [
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' https://fonts.googleapis.com",
            "font-src https://fonts.gstatic.com",
            "img-src 'self'",
            "connect-src 'self'",
            $"form-action 'self' {string.Join(' ', spaOrigins)}".TrimEnd(),
            ancestors.Length == 0 ? "frame-ancestors 'none'" : $"frame-ancestors {string.Join(' ', ancestors)}",
            "base-uri 'none'",
            "object-src 'none'",
        ]);
    }
}
