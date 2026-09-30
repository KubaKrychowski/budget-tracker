using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BudgetTracker.Identity.Infrastructure;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// <c>bt-cli</c> jako klient publiczny (kod + PKCE na loopbacku): to, co da się sprawdzić bez bazy i bez przeglądarki.
/// Sam przepływ (zgoda, przekierowanie na loopback, wymiana kodu, odświeżenie) sprawdzono ręcznie na lokalnym Identity.
/// </summary>
public sealed partial class CliClientTests
{
    private static string ThisDir([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;

    private static string ModulePath =>
        Path.GetFullPath(Path.Combine(ThisDir(), "..", "tools", "bt-cli", "Bt", "Bt.psm1"));

    [Fact]
    public void Loopback_addresses_use_the_ip_literal_not_localhost_or_a_wildcard()
    {
        // RFC 8252 zaleca literał IP: „localhost" bywa rozwiązywany na ::1, a nasłuch moduł otwiera wyłącznie na 127.0.0.1.
        Assert.All(OAuthDefaults.CliLoopbackPorts, port =>
        {
            var uri = new Uri(OAuthDefaults.CliRedirectUri(port));

            Assert.Equal("http", uri.Scheme);
            Assert.Equal("127.0.0.1", uri.Host);
            Assert.Equal(port, uri.Port);
            Assert.Equal("/callback", uri.AbsolutePath);
        });
    }

    [Fact]
    public void The_powershell_module_and_the_server_agree_on_the_loopback_ports()
    {
        // ⚠️ Serwer porównuje redirect_uri DOSŁOWNIE, z portem. Rozjazd list daje wyłącznie błąd „redirect_uri nie jest
        // zarejestrowany" na porcie, który moduł uznał za dobry — bez śladu, dlaczego.
        var module = File.ReadAllText(ModulePath);
        var match = PortList().Match(module);

        Assert.True(match.Success, "Nie znaleziono $script:BtLoopbackPorts w Bt.psm1.");

        var modulePorts = match.Groups["ports"].Value.Split(',', StringSplitOptions.TrimEntries).Select(int.Parse);
        Assert.Equal(OAuthDefaults.CliLoopbackPorts, modulePorts);
    }

    [Fact]
    public void Loopback_origins_are_only_allowed_as_form_action_targets()
    {
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(
            ["https://localhost:4200"], frameableBy: ["https://localhost:4200"],
            extraFormActionOrigins: OAuthDefaults.CliLoopbackOrigins);

        // Logowanie kończy się przekierowaniem na loopback, a Chrome sprawdza form-action także dla przekierowań.
        var formAction = csp.Split("; ").Single(d => d.StartsWith("form-action", StringComparison.Ordinal));
        Assert.All(OAuthDefaults.CliLoopbackOrigins, origin => Assert.Contains(origin, formAction));

        // ...ale loopback nie może trafić tam, gdzie decyduje o osadzaniu strony.
        var frameAncestors = csp.Split("; ").Single(d => d.StartsWith("frame-ancestors", StringComparison.Ordinal));
        Assert.DoesNotContain("127.0.0.1", frameAncestors);
    }

    [Fact]
    public void The_module_holds_no_client_secret_and_does_not_use_the_password_grant()
    {
        var module = File.ReadAllText(ModulePath);

        Assert.DoesNotContain("client_secret", module);
        Assert.DoesNotContain("BT_CLI_CLIENT_SECRET", module);
        Assert.DoesNotContain("grant_type    = 'password'", module);
        Assert.DoesNotContain("Read-Host", module);
    }

    [Fact]
    public void The_module_is_plain_ascii_because_windows_powershell_reads_it_in_the_system_code_page()
    {
        // Zasada repo (CLAUDE.md): ogonki i „—" w .psm1 zamieniają się w krzaki i parser rzuca Unexpected token.
        var offenders = File.ReadAllText(ModulePath).Where(c => c > 127).Distinct().ToArray();

        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"\$script:BtLoopbackPorts\s*=\s*@\((?<ports>[\d,\s]+)\)")]
    private static partial Regex PortList();
}
