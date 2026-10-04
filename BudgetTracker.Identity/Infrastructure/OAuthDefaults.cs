namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Nazwy klientów, zakresu i roszczeń, którymi posługuje się zarówno seeder, jak i kontrolery.
/// <see cref="ApiScope"/> jest też odbiorcą tokenów po stronie API (<c>IdentityServerDefaults.ApiAudience</c>).
/// </summary>
public static class OAuthDefaults
{
    /// <summary>Zakres (i zarazem zasób/odbiorca) tokenów przeznaczonych dla BudgetTracker.Api.</summary>
    public const string ApiScope = "budgettracker_api";

    /// <summary>Klucz konfiguracji z adresem wystawcy tokenów (<c>iss</c>) — bez wartości domyślnej.</summary>
    public const string IssuerConfigKey = "Identity:Issuer";

    public const string SpaClientId = "budgettracker-spa";

    public const string CliClientId = "bt-cli";

    /// <summary>
    /// Porty na 127.0.0.1, na których <c>bt-cli</c> odbiera kod autoryzacyjny (RFC 8252, przekierowanie na loopback).
    /// </summary>
    /// <remarks>
    /// ⚠️ Lista jest zaszyta też w module PowerShell (<c>tools/bt-cli/Bt/Bt.psm1</c>, <c>$BtLoopbackPorts</c>) — obie muszą
    /// się zgadzać, bo OpenIddict porównuje <c>redirect_uri</c> DOSŁOWNIE, z portem. Kilka portów zamiast jednego, żeby
    /// zajęty port nie blokował logowania; CLI bierze pierwszy wolny.
    /// </remarks>
    public static readonly int[] CliLoopbackPorts = [53682, 53683, 53684, 53685, 53686];

    /// <summary>Adres powrotu klienta <c>bt-cli</c> na danym porcie loopback.</summary>
    public static string CliRedirectUri(int port) => $"http://127.0.0.1:{port}/callback";

    /// <summary>Originy loopback klienta <c>bt-cli</c> — potrzebne w <c>form-action</c> CSP, bo logowanie kończy się przekierowaniem na nie.</summary>
    public static IEnumerable<string> CliLoopbackOrigins => CliLoopbackPorts.Select(port => $"http://127.0.0.1:{port}");

    /// <summary>Aplikacja mobilna (Capacitor, Android i iOS) — ten sam front co SPA, ale jako osobny klient publiczny.</summary>
    public const string MobileClientId = "budgettracker-mobile";

    /// <summary>
    /// Własny schemat adresów aplikacji mobilnej (RFC 8252 §7.1: odwrócona domena). Pod nim system operacyjny oddaje
    /// przekierowanie z przeglądarki z powrotem do aplikacji.
    /// </summary>
    /// <remarks>
    /// ⚠️ Zaszyty też w <c>web/capacitor.config.ts</c> (appId), w <c>web/src/app/core/native/native-auth.ts</c>,
    /// w <c>AndroidManifest.xml</c> i w <c>Info.plist</c> — OpenIddict porównuje <c>redirect_uri</c> DOSŁOWNIE.
    /// </remarks>
    public const string MobileScheme = "com.wydatki.app";

    public const string MobileRedirectUri = MobileScheme + ":/auth-callback";

    public const string MobilePostLogoutRedirectUri = MobileScheme + ":/logout";

    /// <summary>
    /// Originy, z których WebView Capacitora woła discovery i endpoint tokenu (fetch, więc CORS): Android serwuje
    /// aplikację spod <c>https://localhost</c>, iOS spod <c>capacitor://localhost</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Idą WYŁĄCZNIE do CORS, nie do listy SPA — ta decyduje też o <c>frame-ancestors</c> i o open redirectach,
    /// a <c>https://localhost</c> to adres, pod którym może stać cokolwiek na komputerze użytkownika.
    /// </remarks>
    public static readonly string[] MobileWebViewOrigins = ["https://localhost", "capacitor://localhost"];

    /// <summary>Klient serwisowy (client credentials), którym serwer tożsamości wywołuje endpointy /api/admin API budżetu.</summary>
    public const string AdminClientId = "budgettracker-admin";

    /// <summary>Zakres tokenów serwisowych; API wymaga go na /api/admin (IdentityServerDefaults.AdminScope po stronie API).</summary>
    public const string AdminApiScope = "budgettracker_admin";

    /// <summary>Klucz konfiguracji z sekretem klienta serwisowego — user-secrets / zmienne środowiskowe, nigdy repo.</summary>
    public const string AdminClientSecretConfigKey = "Clients:Admin:Secret";

    /// <summary>
    /// Niestandardowe roszczenie odczytywane przez ekran „Konto i bezpieczeństwo" w Angularze
    /// (z <c>userData</c> OIDC) — front nie ma dostępu do bazy Identity, więc status 2FA musi
    /// jechać w tokenie. Wartość to dosłowne "true"/"false", nie C#-owe "True"/"False".
    /// </summary>
    public const string TwoFactorEnabledClaimType = "two_factor_enabled";
}
