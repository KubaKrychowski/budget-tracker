using System.Runtime.CompilerServices;
using BudgetTracker.Identity.Infrastructure;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Aplikacja mobilna (Capacitor) jako klient publiczny z powrotem przez własny schemat adresu: to, co da się sprawdzić bez
/// bazy i bez telefonu. Sam przepływ (systemowa przeglądarka, deep link, wymiana kodu) sprawdza się na emulatorze.
/// </summary>
public sealed class MobileClientTests
{
    private static string ThisDir([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;

    private static string RepoFile(params string[] parts) =>
        Path.GetFullPath(Path.Combine([ThisDir(), "..", .. parts]));

    [Fact]
    public void Redirect_uris_use_the_private_scheme_from_rfc_8252()
    {
        // RFC 8252 §7.1: odwrócona domena, którą kontrolujemy, i JEDEN ukośnik po dwukropku.
        var redirect = new Uri(OAuthDefaults.MobileRedirectUri);
        var logout = new Uri(OAuthDefaults.MobilePostLogoutRedirectUri);

        Assert.Equal(OAuthDefaults.MobileScheme, redirect.Scheme);
        Assert.Equal(OAuthDefaults.MobileScheme, logout.Scheme);
        Assert.Equal("com.wydatki.app:/auth-callback", OAuthDefaults.MobileRedirectUri);
    }

    [Fact]
    public void Login_may_redirect_to_the_app_scheme_but_the_app_cannot_frame_identity()
    {
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(
            ["https://localhost:4200"], frameableBy: ["https://localhost:4200"],
            extraFormActionOrigins: SecurityHeadersMiddleware.NativeClientFormActionTargets);

        // Logowanie i zgoda kończą się przekierowaniem na com.wydatki.app:/auth-callback, a Chrome sprawdza form-action
        // także dla przekierowań — bez schematu w CSP przeglądarka nie odda kodu aplikacji.
        var formAction = csp.Split("; ").Single(d => d.StartsWith("form-action", StringComparison.Ordinal));
        Assert.Contains("com.wydatki.app:", formAction);

        var frameAncestors = csp.Split("; ").Single(d => d.StartsWith("frame-ancestors", StringComparison.Ordinal));
        Assert.DoesNotContain("com.wydatki.app", frameAncestors);
        Assert.DoesNotContain("capacitor://", frameAncestors);
    }

    [Theory]
    [InlineData("web", "capacitor.config.ts")]
    [InlineData("web", "src", "app", "core", "native", "native-auth.ts")]
    [InlineData("web", "android", "app", "src", "main", "AndroidManifest.xml")]
    [InlineData("web", "ios", "App", "App", "Info.plist")]
    public void Every_native_project_file_registers_the_same_scheme(params string[] path)
    {
        // ⚠️ OpenIddict porównuje redirect_uri DOSŁOWNIE, a system oddaje adres tylko aplikacji, która zgłosiła schemat.
        // Rozjazd daje „redirect_uri nie jest zarejestrowany" albo przeglądarkę, która po zalogowaniu nie wraca do apki.
        Assert.Contains(OAuthDefaults.MobileScheme, File.ReadAllText(RepoFile(path)));
    }

    [Fact]
    public void The_api_accepts_requests_from_both_webviews_in_production()
    {
        // Front w apce woła API z originu WebView — bez tych wpisów każde żądanie kończy się błędem CORS na telefonie.
        var terraform = File.ReadAllText(RepoFile("infra", "app-service.tf"));

        Assert.All(OAuthDefaults.MobileWebViewOrigins, origin => Assert.Contains($"\"{origin}\"", terraform));
    }
}
