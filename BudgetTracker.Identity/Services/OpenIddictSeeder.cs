using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Options;
using BudgetTracker.Identity.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BudgetTracker.Identity.Services;

/// <summary>
/// Zakłada przy starcie klientów OAuth wymaganych przez resztę systemu: SPA Angulara (kod + PKCE)
/// <c>bt-cli</c> (kod + PKCE na loopbacku, klient publiczny — patrz DECISIONS.md) i aplikację mobilną (kod + PKCE, powrót przez własny schemat adresu). W Development zakłada też konta dev/demo — z TYM SAMYM identyfikatorem, którym
/// <c>DevSeed</c>/<c>DemoSeed</c> w API oznaczają zaseedowane budżety (patrz <see cref="DeterministicGuid"/>),
/// inaczej zalogowany dev/demo user nie zobaczyłby własnych danych. Idempotentne — sprawdza istnienie
/// przed utworzeniem.
/// </summary>
public sealed class OpenIddictSeeder(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    FixedAccountSeeder fixedAccounts,
    IOptions<SpaClientOptions> spaClient,
    IWebHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        await SeedScopeAsync(scopeManager, cancellationToken);

        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await SeedSpaClientAsync(appManager, cancellationToken);
        await SeedCliClientAsync(appManager, cancellationToken);
        await SeedMobileClientAsync(appManager, cancellationToken);
        await SeedAdminClientAsync(appManager, cancellationToken);

        if (environment.IsDevelopment())
        {
            // Brak hasła albo hasło odrzucone przez politykę to ostrzeżenie w konsoli, nie błąd startu (patrz FixedAccountSeeder).
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await fixedAccounts.SeedAsync(
                userManager, DeterministicGuid.For("dev:user:owner"), "dev@budgettracker.local", "Seed:DevPassword");
            await fixedAccounts.SeedAsync(
                userManager, DeterministicGuid.For("demo:user:owner"), "demo@budgettracker.local", "Seed:DemoPassword");
        }

        // Rola admin po utworzeniu kont dev/demo, żeby adres z Admin:Emails, który akurat jest kontem seedowanym,
        // dostał ją już przy pierwszym starcie.
        await scope.ServiceProvider.GetRequiredService<AdminRoleService>().GrantConfiguredAdminsAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedScopeAsync(IOpenIddictScopeManager scopeManager, CancellationToken ct)
    {
        await SeedAdminScopeAsync(scopeManager, ct);

        if (await scopeManager.FindByNameAsync(OAuthDefaults.ApiScope, ct) is not null) return;

        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = OAuthDefaults.ApiScope,
            DisplayName = "Wydatki.com API",
            Resources = { OAuthDefaults.ApiScope },
        }, ct);
    }

    /// <summary>
    /// Zakres tokenów serwisowych. Zasobem (odbiorcą, <c>aud</c>) jest to samo API budżetu — endpointy /api/admin są jego
    /// częścią i odróżnia je dopiero zakres, którego zwykłe tokeny użytkowników nie mają.
    /// </summary>
    private static async Task SeedAdminScopeAsync(IOpenIddictScopeManager scopeManager, CancellationToken ct)
    {
        if (await scopeManager.FindByNameAsync(OAuthDefaults.AdminApiScope, ct) is not null) return;

        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = OAuthDefaults.AdminApiScope,
            DisplayName = "Wydatki.com API — polecenia administracyjne",
            Resources = { OAuthDefaults.ApiScope },
        }, ct);
    }

    private async Task SeedSpaClientAsync(IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        const string clientId = OAuthDefaults.SpaClientId;

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "Wydatki.com — aplikacja webowa",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            // Bez GrantTypes.RefreshToken/scope "offline_access" — SPA odnawia sesję ukrytym
            // iframe'em (silent renew, ciasteczko logowania z Identity), nie refresh tokenem.
            // Żaden długożyjący token do odnawiania sesji nie trafia więc do storage przeglądarki.
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + OAuthDefaults.ApiScope,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        foreach (var uri in spaClient.Value.RedirectUris) descriptor.RedirectUris.Add(new Uri(uri));
        foreach (var uri in spaClient.Value.PostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

        // Konfiguracja jest źródłem prawdy przy KAŻDYM starcie, nie tylko przy pierwszym: zmiana adresów
        // (np. przejście z http na https) musi dojść do klienta zapisanego już w bazie, inaczej Identity dalej
        // odrzucałoby nowy redirect_uri jako niezarejestrowany.
        if (await appManager.FindByClientIdAsync(clientId, ct) is { } existing)
        {
            await appManager.UpdateAsync(existing, descriptor, ct);
            return;
        }

        await appManager.CreateAsync(descriptor, ct);
    }

    /// <summary>
    /// Klient serwisowy, którym serwer tożsamości woła /api/admin API budżetu (client credentials). Jedyny z zezwoleniem
    /// na zakres <c>budgettracker_admin</c>. Uzgadniany przy KAŻDYM starcie (jak klient SPA), żeby zmiana sekretu
    /// w konfiguracji dochodziła do bazy; sekret bez wartości domyślnej — wymagany w każdym środowisku.
    /// </summary>
    private async Task SeedAdminClientAsync(IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        var secret = configuration[OAuthDefaults.AdminClientSecretConfigKey]
            ?? throw new InvalidOperationException(
                $"Brak {OAuthDefaults.AdminClientSecretConfigKey} w konfiguracji. Ustaw: dotnet user-secrets set \"{OAuthDefaults.AdminClientSecretConfigKey}\" \"...\"");

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = OAuthDefaults.AdminClientId,
            ClientSecret = secret,
            DisplayName = "Serwer tożsamości — polecenia administracyjne API",
            ClientType = ClientTypes.Confidential,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + OAuthDefaults.AdminApiScope,
            },
        };

        if (await appManager.FindByClientIdAsync(OAuthDefaults.AdminClientId, ct) is { } existing)
        {
            await appManager.UpdateAsync(existing, descriptor, ct);
            return;
        }

        await appManager.CreateAsync(descriptor, ct);
    }

    /// <summary>
    /// <c>bt-cli</c> jako klient PUBLICZNY: kod autoryzacyjny + PKCE z przekierowaniem na loopback (RFC 8252), bez sekretu
    /// i bez grantu hasła. Uzgadniany przy KAŻDYM starcie (jak klient SPA), więc istniejący klient z sekretem i grantem hasła
    /// zostaje przy wdrożeniu przekształcony — sekret znika z bazy.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Bez sekretu, bo CLI jest dla WSZYSTKICH użytkowników: sekret rozdany każdemu nie jest sekretem, a wgrywany
    /// do kopii CLI byłby do wyciągnięcia z dysku. Klient publiczny nie udaje, że coś chroni — ochroną jest PKCE.</item>
    /// <item>Hasło nie przechodzi przez CLI: użytkownik loguje się na zwykłej stronie Identity, więc działa też 2FA
    /// (grant hasła odrzucał konta z 2FA).</item>
    /// <item>⚠️ Zgoda JAWNA (jak w SPA), nie domyślna: klient publiczny na loopbacku może się podszyć pod <c>bt-cli</c>
    /// dowolna lokalna aplikacja, a ekran zgody to jedyny moment, w którym użytkownik widzi, komu daje dostęp.
    /// Zgoda zapisuje się trwale, więc pytanie pada raz.</item>
    /// <item>Zakres <c>offline_access</c> zostaje: CLI odświeża sesję w tle, bo nie ma jak zrobić cichego iframe'a.</item>
    /// </list>
    /// </remarks>
    private static async Task SeedCliClientAsync(IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = OAuthDefaults.CliClientId,
            DisplayName = "bt-cli (PowerShell)",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "offline_access",
                Permissions.Prefixes.Scope + OAuthDefaults.ApiScope,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        foreach (var port in OAuthDefaults.CliLoopbackPorts) descriptor.RedirectUris.Add(new Uri(OAuthDefaults.CliRedirectUri(port)));

        if (await appManager.FindByClientIdAsync(OAuthDefaults.CliClientId, ct) is { } existing)
        {
            await appManager.UpdateAsync(existing, descriptor, ct);
            return;
        }

        await appManager.CreateAsync(descriptor, ct);
    }

    /// <summary>
    /// Aplikacja mobilna (Capacitor) jako klient PUBLICZNY: kod + PKCE, logowanie w systemowej przeglądarce i powrót przez
    /// własny schemat adresu (RFC 8252). Uzgadniany przy KAŻDYM starcie, jak pozostali klienci.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Osobny klient, nie dodatkowe adresy SPA: ma refresh token, którego SPA świadomie nie dostaje. WebView nie
    /// zrobi cichego iframe'a — ciasteczko sesji Identity żyje w systemowej przeglądarce, nie w aplikacji.</item>
    /// <item>⚠️ Zgoda JAWNA: dowolna aplikacja na telefonie może zarejestrować ten sam schemat adresu, więc ekran zgody
    /// to jedyny moment, w którym użytkownik widzi, komu daje dostęp. Ochroną samego kodu jest PKCE.</item>
    /// </list>
    /// </remarks>
    private static async Task SeedMobileClientAsync(IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = OAuthDefaults.MobileClientId,
            DisplayName = "Wydatki.com — aplikacja mobilna",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "offline_access",
                Permissions.Prefixes.Scope + OAuthDefaults.ApiScope,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
            RedirectUris = { new Uri(OAuthDefaults.MobileRedirectUri) },
            PostLogoutRedirectUris = { new Uri(OAuthDefaults.MobilePostLogoutRedirectUri) },
        };

        if (await appManager.FindByClientIdAsync(OAuthDefaults.MobileClientId, ct) is { } existing)
        {
            await appManager.UpdateAsync(existing, descriptor, ct);
            return;
        }

        await appManager.CreateAsync(descriptor, ct);
    }
}
