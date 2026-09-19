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
