using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BudgetTracker.Identity.Services;

/// <summary>
/// Zakłada przy starcie klientów OAuth wymaganych przez resztę systemu: SPA Angulara (kod + PKCE)
/// i <c>bt-cli</c> (hasło — zaufany klient pierwszej strony, jedyny użytkownik jest też jedynym deweloperem,
/// patrz DECISIONS.md). W Development zakłada też konta dev/demo — z TYM SAMYM identyfikatorem, którym
/// <c>DevSeed</c>/<c>DemoSeed</c> w API oznaczają zaseedowane budżety (patrz <see cref="DeterministicGuid"/>),
/// inaczej zalogowany dev/demo user nie zobaczyłby własnych danych. Idempotentne — sprawdza istnienie
/// przed utworzeniem.
/// </summary>
public sealed class OpenIddictSeeder(IServiceProvider serviceProvider, IConfiguration configuration, IWebHostEnvironment environment) : IHostedService
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

        if (environment.IsDevelopment())
        {
            // Hasła NIE mają defaultu w kodzie ani w appsettings (trafiłyby do gita) — trzymaj je
            // w user-secrets: `dotnet user-secrets set "Seed:DevPassword" "..."` (i analogicznie
            // Seed:DemoPassword, Clients:Cli:Secret). Patrz README katalogu.
            var devPassword = configuration["Seed:DevPassword"]
                ?? throw new InvalidOperationException(
                    "Brak Seed:DevPassword w konfiguracji. Ustaw: dotnet user-secrets set \"Seed:DevPassword\" \"...\"");
            var demoPassword = configuration["Seed:DemoPassword"]
                ?? throw new InvalidOperationException(
                    "Brak Seed:DemoPassword w konfiguracji. Ustaw: dotnet user-secrets set \"Seed:DemoPassword\" \"...\"");

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await SeedFixedUserAsync(
                userManager, DeterministicGuid.For("dev:user:owner"), "dev@budgettracker.local", devPassword);
            await SeedFixedUserAsync(
                userManager, DeterministicGuid.For("demo:user:owner"), "demo@budgettracker.local", demoPassword);
        }
    }

    /// <summary>
    /// Konto z GÓRY NARZUCONYM identyfikatorem — musi pasować do <c>Budget.UserId</c> zaseedowanego
    /// w API, więc nie może dostać losowego <see cref="Guid"/> z domyślnego <c>CreateAsync</c>.
    /// </summary>
    private static async Task SeedFixedUserAsync(
        UserManager<ApplicationUser> userManager, Guid id, string email, string password)
    {
        if (await userManager.FindByIdAsync(id.ToString()) is not null) return;

        var user = new ApplicationUser { Id = id, UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Nie udało się założyć konta {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedScopeAsync(IOpenIddictScopeManager scopeManager, CancellationToken ct)
    {
        if (await scopeManager.FindByNameAsync("budgettracker_api", ct) is not null) return;

        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "budgettracker_api",
            DisplayName = "Budżet tracker API",
            Resources = { "budgettracker_api" },
        }, ct);
    }

    private async Task SeedSpaClientAsync(IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        const string clientId = "budgettracker-spa";
        if (await appManager.FindByClientIdAsync(clientId, ct) is not null) return;

        var redirectUris = configuration.GetSection("Clients:Spa:RedirectUris").Get<string[]>()
            ?? ["http://localhost:4200/auth-callback", "http://localhost:4310/auth-callback"];
        var postLogoutRedirectUris = configuration.GetSection("Clients:Spa:PostLogoutRedirectUris").Get<string[]>()
            ?? ["http://localhost:4200/", "http://localhost:4310/"];

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "Budżet tracker — aplikacja webowa",
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
                Permissions.Prefixes.Scope + "budgettracker_api",
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        foreach (var uri in redirectUris) descriptor.RedirectUris.Add(new Uri(uri));
        foreach (var uri in postLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

        await appManager.CreateAsync(descriptor, ct);
    }

    private async Task SeedCliClientAsync(IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        const string clientId = "bt-cli";
        if (await appManager.FindByClientIdAsync(clientId, ct) is not null) return;

        // Sekret deweloperski — jedyny użytkownik jest też jedynym deweloperem (CLAUDE.md), więc
        // "bt-cli" to zaufany klient pierwszej strony uruchamiany lokalnie, nie publiczna integracja.
        // BEZ defaultu w kodzie/appsettings (trafiłby do gita) — patrz README `tools/bt-cli/`.
        var secret = configuration["Clients:Cli:Secret"]
            ?? throw new InvalidOperationException(
                "Brak Clients:Cli:Secret w konfiguracji. Ustaw: dotnet user-secrets set \"Clients:Cli:Secret\" \"...\"");

        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            DisplayName = "bt-cli (PowerShell)",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "offline_access",
                Permissions.Prefixes.Scope + "budgettracker_api",
            },
        }, ct);
    }
}
