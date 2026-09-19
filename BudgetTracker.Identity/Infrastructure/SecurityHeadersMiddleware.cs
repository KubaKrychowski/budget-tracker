using BudgetTracker.Identity.Services;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Nagłówki bezpieczeństwa dla wszystkich odpowiedzi Identity. Najważniejszy jest CSP: ekrany logowania są
/// jedynym miejscem, gdzie użytkownik wpisuje hasło i kod 2FA, więc wstrzyknięty skrypt = przejęte konto.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, SpaOrigins spaOrigins)
{
    private readonly string contentSecurityPolicy = BuildContentSecurityPolicy(spaOrigins.All);

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = contentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        // Kod autoryzacyjny jedzie w adresie; bez tego nagłówka mógłby wyciec w Referer do zewnętrznego zasobu.
        headers["Referrer-Policy"] = "no-referrer";

        return next(context);
    }

    /// <summary>
    /// <c>script-src 'self'</c> bez <c>unsafe-inline</c>: widoki nie mają skryptów ani stylów inline (patrz
    /// <c>auth.css</c>). Czcionki idą z Google Fonts, reszta tylko z własnego originu.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>form-action</c> zawiera originy SPA, bo Chrome sprawdza je także dla PRZEKIEROWAŃ po wysłaniu formularza:
    /// logowanie i zgoda kończą się przekierowaniem na <c>redirect_uri</c> frontu, więc bez tego przeglądarka
    /// zablokowałaby powrót do aplikacji.
    /// </remarks>
    public static string BuildContentSecurityPolicy(IEnumerable<string> spaOrigins) =>
        string.Join("; ",
        [
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' https://fonts.googleapis.com",
            "font-src https://fonts.gstatic.com",
            "img-src 'self'",
            "connect-src 'self'",
            $"form-action 'self' {string.Join(' ', spaOrigins)}".TrimEnd(),
            "frame-ancestors 'none'",
            "base-uri 'none'",
            "object-src 'none'",
        ]);
}
