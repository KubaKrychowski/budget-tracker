namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Ustalenia z serwerem tożsamości (BudgetTracker.Identity), których API musi się trzymać razem z nim.
/// </summary>
public static class IdentityServerDefaults
{
    /// <summary>Klucz konfiguracji z adresem wystawcy tokenów (<c>iss</c>) — bez wartości domyślnej, patrz <c>Program.cs</c>.</summary>
    public const string IssuerConfigKey = "Identity:Issuer";

    /// <summary>Odbiorca (<c>aud</c>) tokenów przeznaczonych dla tego API — ten sam, co zakres zakładany w Identity.</summary>
    public const string ApiAudience = "budgettracker_api";

    /// <summary>Zakres tokenów serwisowych serwera tożsamości (client credentials) do endpointów <c>/api/admin</c>.</summary>
    public const string AdminScope = "budgettracker_admin";
}
