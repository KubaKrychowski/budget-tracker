using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Options;
using Microsoft.Extensions.Configuration;

namespace BudgetTracker.Identity.Tests;

public sealed partial class SecurityHardeningTests
{
    [Fact]
    public void Content_security_policy_forbids_inline_and_eval_scripts_and_framing()
    {
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(["https://localhost:4200"]);

        Assert.Contains("script-src 'self'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("base-uri 'none'", csp);
    }

    [Fact]
    public void Form_action_lists_the_spa_origins_so_the_redirect_back_to_the_app_is_not_blocked()
    {
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(["https://localhost:4200", "https://localhost:4310"]);

        Assert.Contains("form-action 'self' https://localhost:4200 https://localhost:4310", csp);
    }

    [Fact]
    public void Content_security_policy_is_well_formed_without_any_registered_spa()
    {
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy([]);

        Assert.Contains("form-action 'self';", csp);
    }

    [Fact]
    public void The_authorization_endpoint_may_be_framed_by_the_spa_and_by_nobody_else()
    {
        // ⚠️ Ciche odnawianie sesji to ukryta ramka na /connect/authorize. Przy `frame-ancestors 'none'`
        // przeglądarka blokuje ją BEZ SLADU w logach serwera, a jedynym objawem jest 401 po wygaśnięciu
        // tokenu — tak było do 2026-09-26 i nie działało nawet lokalnie.
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(
            ["https://localhost:4200"], frameableBy: ["https://localhost:4200"]);

        Assert.Contains("frame-ancestors https://localhost:4200", csp);
        Assert.DoesNotContain("frame-ancestors 'none'", csp);

        // Gwiazdka zamiast listy adresów otworzyłaby punkt autoryzacji na osadzenie przez kogokolwiek.
        Assert.DoesNotContain("frame-ancestors *", csp);
    }

    [Fact]
    public void Without_a_frameable_origin_the_policy_still_forbids_framing()
    {
        // Pusta lista nie ma znaczyć „wszyscy". To jest ten rodzaj pomyłki, który nie daje żadnego objawu.
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(["https://localhost:4200"], frameableBy: []);

        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [Fact]
    public void Views_have_no_inline_styles_scripts_or_event_handlers_that_the_csp_would_block()
    {
        var viewsDir = Path.GetFullPath(Path.Combine(ThisDir(), "..", "BudgetTracker.Identity", "Views"));
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var name = Path.GetRelativePath(viewsDir, file);

            if (InlineStyleAttribute().IsMatch(text)) offenders.Add($"{name}: style=\"\"");
            if (InlineScriptBlock().IsMatch(text)) offenders.Add($"{name}: <script> bez src");
            if (InlineEventHandler().IsMatch(text)) offenders.Add($"{name}: atrybut onXxx=");
        }

        Assert.True(offenders.Count == 0, "CSP zablokuje: " + string.Join("; ", offenders));
    }

    [Fact]
    public void Access_token_defaults_are_short_because_the_api_cannot_revoke_them()
    {
        var defaults = new TokenLifetimeOptions();

        Assert.True(defaults.AccessToken <= TimeSpan.FromMinutes(30), "access token żyje zbyt długo");
        Assert.True(defaults.IdentityToken <= TimeSpan.FromMinutes(30), "id token żyje zbyt długo");
    }

    [Fact]
    public void Token_lifetimes_bind_from_configuration_in_timespan_format()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tokens:AccessToken"] = "00:01:00",
                ["Tokens:RefreshToken"] = "7.00:00:00",
            })
            .Build();

        var options = config.GetSection(TokenLifetimeOptions.SectionName).Get<TokenLifetimeOptions>()!;

        Assert.Equal(TimeSpan.FromMinutes(1), options.AccessToken);
        Assert.Equal(TimeSpan.FromDays(7), options.RefreshToken);
        // Niepodane wartości zostają przy domyślnych.
        Assert.Equal(new TokenLifetimeOptions().IdentityToken, options.IdentityToken);
    }

    private static string ThisDir([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;

    [GeneratedRegex(@"\sstyle\s*=\s*""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyleAttribute();

    [GeneratedRegex(@"<script(?![^>]*\ssrc\s*=)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScriptBlock();

    [GeneratedRegex(@"\son[a-z]+\s*=\s*""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineEventHandler();
}
