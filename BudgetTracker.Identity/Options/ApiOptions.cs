namespace BudgetTracker.Identity.Options;

/// <summary>Adres API budżetu, do którego serwer tożsamości wysyła polecenia administracyjne (usuwanie danych kont).</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Bez wartości domyślnej: dev/demo mają ją w <c>appsettings.Development.json</c>, reszta musi ją podać.</summary>
    public string BaseUrl { get; init; } = "";

    public bool HasValidBaseUrl =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
