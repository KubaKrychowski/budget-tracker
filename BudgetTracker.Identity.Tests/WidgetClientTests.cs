using System.Runtime.CompilerServices;
using BudgetTracker.Identity.Infrastructure;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Widżet limitów na pulpicie Androida jako osobny klient publiczny: to, co da się sprawdzić bez bazy i telefonu.
/// </summary>
public sealed class WidgetClientTests
{
    private static string ThisDir([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;

    private static string RepoFile(params string[] parts) =>
        Path.GetFullPath(Path.Combine([ThisDir(), "..", .. parts]));

    [Fact]
    public void Widget_is_a_separate_client_from_the_app()
    {
        // Wspólny klient znaczyłby wspólny refresh token, a OpenIddict obraca te tokeny: drugie użycie zużytego
        // tokena unieważnia autoryzację i wylogowuje użytkownika z aplikacji.
        Assert.NotEqual(OAuthDefaults.MobileClientId, OAuthDefaults.WidgetClientId);
    }

    [Fact]
    public void Widget_scheme_differs_from_the_app_scheme_so_the_deep_link_reaches_the_widget()
    {
        var redirect = new Uri(OAuthDefaults.WidgetRedirectUri);

        Assert.Equal(OAuthDefaults.WidgetScheme, redirect.Scheme);
        Assert.NotEqual(OAuthDefaults.MobileScheme, redirect.Scheme);
        Assert.Equal("com.wydatki.app.widget:/callback", OAuthDefaults.WidgetRedirectUri);
    }

    [Fact]
    public void Login_may_redirect_to_the_widget_scheme()
    {
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(
            ["https://localhost:4200"],
            extraFormActionOrigins: SecurityHeadersMiddleware.NativeClientFormActionTargets);

        var formAction = csp.Split("; ").Single(d => d.StartsWith("form-action", StringComparison.Ordinal));
        Assert.Contains("com.wydatki.app.widget:", formAction);
    }

    [Theory]
    [InlineData("web", "android", "app", "src", "main", "AndroidManifest.xml")]
    [InlineData("web", "android", "app", "src", "main", "java", "com", "wydatki", "app", "widget", "WidgetConfig.java")]
    public void Native_files_use_the_same_widget_scheme_and_client(params string[] path)
    {
        // ⚠️ redirect_uri jest porównywany DOSŁOWNIE; rozjazd to „redirect_uri nie jest zarejestrowany".
        var text = File.ReadAllText(RepoFile(path));

        Assert.Contains(OAuthDefaults.WidgetScheme, text);
        if (path[^1].EndsWith(".java", StringComparison.Ordinal))
            Assert.Contains(OAuthDefaults.WidgetClientId, text);
    }
}
