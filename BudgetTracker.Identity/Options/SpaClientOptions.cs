namespace BudgetTracker.Identity.Options;

/// <summary>
/// Adresy klienta SPA (Angular) zarejestrowane w OpenIddict. Bez wartości domyślnych w kodzie —
/// dev/demo mają je w <c>appsettings.Development.json</c>, pozostałe środowiska muszą je podać same.
/// </summary>
public sealed class SpaClientOptions
{
    public const string SectionName = "Clients:Spa";

    public string[] RedirectUris { get; init; } = [];

    public string[] PostLogoutRedirectUris { get; init; } = [];
}
