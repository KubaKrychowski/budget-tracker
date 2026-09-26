using System.Collections.Immutable;
using System.Security.Claims;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Resources;
using Microsoft.AspNetCore; // GetOpenIddictServerRequest() itp. — helpery leżą w tej przestrzeni nazw w 8.0-preview.
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BudgetTracker.Identity.Controllers;

/// <summary>
/// Endpointy <c>/connect/*</c> serwera OpenIddict: ekran zgody (authorize), wymiana kodu/odświeżenia
/// i logowania hasłem na token (token), oraz wylogowanie RP-initiated (logout).
/// <para>
/// Ekran zgody, kod + PKCE i wymiana kodu/odświeżenia wzorowane na przykładzie Velusia:
/// <see href="https://github.com/openiddict/openiddict-samples/blob/dev/samples/Velusia/Velusia.Server/Controllers/AuthorizationController.cs"/>.
/// Dopasowane pod ASP.NET Identity jako magazyn kont; grant hasła (bt-cli) jest naszym dodatkiem, nie częścią tego przykładu.
/// </para>
/// </summary>
public sealed class AuthorizationController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictScopeManager scopeManager,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpGet("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("Żądanie OpenIddict nie zostało poprawnie zainicjalizowane.");

        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = result is { Succeeded: true } ? await userManager.GetUserAsync(result.Principal) : null;
        if (user is null)
        {
            // Ciasteczko może wskazywać konto, którego już nie ma (usunięte w trakcie sesji) — to samo co brak
            // logowania, nie awaria. Kasujemy je, żeby nie wracało przy każdym kolejnym żądaniu.
            if (result is { Succeeded: true }) await signInManager.SignOutAsync();

            // Silent renew (ukryty iframe, patrz SPA) prosi z prompt=none: pokazanie ekranu logowania
            // w ukrytej ramce zawiesiłoby żądanie na dobre, więc zamiast Challenge() od razu wraca
            // błąd — front sam wie, że trzeba przelogować interaktywnie.
            if (request.HasPromptValue(PromptValues.None))
            {
                return ForbidWithError(Errors.LoginRequired, localizer["OAuth_NotSignedIn"]);
            }

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType ? Request.Form.ToList()! : Request.Query.ToList()!),
                });
        }

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("Nieznana aplikacja kliencka.");

        var authorizations = await FindAuthorizationsAsync(user, application, request.GetScopes());

        var consentType = await applicationManager.GetConsentTypeAsync(application);

        var requiresConsent = consentType switch
        {
            ConsentTypes.Implicit => false,
            ConsentTypes.External => authorizations.Count == 0,
            _ => authorizations.Count == 0 || request.HasPromptValue(PromptValues.Consent),
        };

        if (requiresConsent)
        {
            // To samo co wyżej: silent renew nie może pokazać interaktywnego ekranu zgody w ukrytej
            // ramce. Pierwsze logowanie zawsze idzie pełną nawigacją (tam prompt=none nie występuje),
            // więc do tego miejsca z prompt=none trafiamy tylko, gdy zgoda jeszcze nie istnieje.
            if (request.HasPromptValue(PromptValues.None))
            {
                return ForbidWithError(Errors.ConsentRequired, localizer["OAuth_ConsentMissing"]);
            }

            return View("Authorize", new AuthorizeViewModel
            {
                ApplicationName = await applicationManager.GetDisplayNameAsync(application) ?? request.ClientId!,
                Scopes = request.GetScopes().Except([Scopes.OpenId, Scopes.OfflineAccess]).ToImmutableArray(),
                UserEmail = await userManager.GetEmailAsync(user) ?? "",
            });
        }

        return await IssueSignInAsync(request, user, application, authorizations.FirstOrDefault());
    }

    [Authorize]
    [HttpPost("~/connect/authorize"), ActionName(nameof(Authorize))]
    [FormValueRequired("submit.Accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("Żądanie OpenIddict nie zostało poprawnie zainicjalizowane.");

        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("Zalogowany użytkownik nie istnieje już w bazie.");

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("Nieznana aplikacja kliencka.");

        var authorizations = await FindAuthorizationsAsync(user, application, request.GetScopes());

        return await IssueSignInAsync(request, user, application, authorizations.FirstOrDefault());
    }

    /// <summary>
    /// Owija <see cref="IOpenIddictAuthorizationManager.FindAsync"/> — w 8.0-preview przyjmuje krotkę
    /// zamiast nazwanych parametrów i zwraca <c>IAsyncEnumerable</c>, więc trzeba ręcznie zebrać wynik.
    /// </summary>
    private async Task<List<object>> FindAuthorizationsAsync(
        ApplicationUser user, object application, ImmutableArray<string> scopes)
    {
        var subject = await userManager.GetUserIdAsync(user);
        var client = (await applicationManager.GetIdAsync(application))!.ToString()!;

        var results = new List<object>();
        await foreach (var authorization in authorizationManager.FindAsync(
            (subject, client, Statuses.Valid, AuthorizationTypes.Permanent, scopes)))
        {
            results.Add(authorization);
        }

        return results;
    }

    [Authorize]
    [HttpPost("~/connect/authorize"), ActionName(nameof(Authorize))]
    [FormValueRequired("submit.Deny")]
    [ValidateAntiForgeryToken]
    public IActionResult Deny() => ForbidWithError(Errors.AccessDenied, localizer["OAuth_Denied"]);

    /// <summary>Odpowiedź błędem OAuth zamiast przekierowania/UI — patrz obsługa <c>prompt=none</c> wyżej.</summary>
    private IActionResult ForbidWithError(string error, string description) =>
        Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }));

    private async Task<IActionResult> IssueSignInAsync(
        OpenIddictRequest request,
        ApplicationUser user,
        object application,
        object? existingAuthorization)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        await AddUserClaimsAsync(principal, user);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(await ListResourcesAsync(principal.GetScopes()));

        object authorization;
        if (existingAuthorization is not null)
        {
            authorization = existingAuthorization;
        }
        else
        {
            var descriptor = new OpenIddictAuthorizationDescriptor
            {
                Principal = principal,
                Subject = await userManager.GetUserIdAsync(user),
                ApplicationId = (await applicationManager.GetIdAsync(application))!.ToString(),
                Type = AuthorizationTypes.Permanent,
            };
            foreach (var scope in principal.GetScopes()) descriptor.Scopes.Add(scope);

            authorization = await authorizationManager.CreateAsync(descriptor);
        }

        principal.SetAuthorizationId((await authorizationManager.GetIdAsync(authorization))!.ToString());

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Roszczenia użytkownika w tokenach: identyfikator, e-mail, status 2FA i role.</summary>
    /// <remarks>
    /// ⚠️ <c>CreateUserPrincipalAsync</c> dokłada e-mail i role pod długimi URI ASP.NET Identity
    /// (<c>ClaimTypes.Email</c>, <c>ClaimTypes.Role</c>), nie pod krótkimi nazwami OpenIddict — bez jawnego zapisu
    /// tutaj <see cref="GetDestinations"/> nigdy nie trafia w `case Claims.Email` / `Claims.Role` i nic z tego nie
    /// wchodzi do tokenu. Rola `admin` w id_tokenie jest tym, po czym Angular pokazuje sekcję „Administracja".
    /// </remarks>
    private async Task AddUserClaimsAsync(ClaimsPrincipal principal, ApplicationUser user)
    {
        principal.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user));
        principal.SetClaim(Claims.Email, await userManager.GetEmailAsync(user));
        principal.SetClaim(OAuthDefaults.TwoFactorEnabledClaimType, await userManager.GetTwoFactorEnabledAsync(user) ? "true" : "false");
        principal.SetClaims(Claims.Role, [.. await userManager.GetRolesAsync(user)]);
    }

    /// <summary>Zbiera <c>IAsyncEnumerable</c> z <see cref="IOpenIddictScopeManager.ListResourcesAsync"/> w listę.</summary>
    private async Task<List<string>> ListResourcesAsync(ImmutableArray<string> scopes)
    {
        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(scopes))
        {
            resources.Add(resource);
        }

        return resources;
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("Żądanie OpenIddict nie zostało poprawnie zainicjalizowane.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var user = await userManager.GetUserAsync(result.Principal!);
            if (user is null)
            {
                return ForbidWithError(Errors.InvalidGrant, localizer["OAuth_TokenNoLongerValid"]);
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            await AddUserClaimsAsync(principal, user);
            principal.SetScopes(result.Principal!.GetScopes());
            principal.SetResources(await ListResourcesAsync(principal.GetScopes()));
            principal.SetAuthorizationId(result.Principal!.GetAuthorizationId());

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsPasswordGrantType())
        {
            // Klient zaufany pierwszej strony (bt-cli) — patrz DECISIONS.md: jedyny użytkownik
            // jest też jedynym deweloperem, więc grant "password" jest tu świadomym uproszczeniem,
            // nie ogólną praktyką dla klientów zewnętrznych.
            var user = await userManager.FindByEmailAsync(request.Username!);
            if (user is null)
            {
                return ForbidWithError(Errors.InvalidGrant, localizer["Login_InvalidCredentials"]);
            }

            // ⚠️ Lockout egzekwowany RĘCZNIE, bo goły CheckPasswordAsync go nie rusza — bez tego ROPuC był
            // kanałem na nieograniczone zgadywanie hasła, omijającym blokadę po 3 próbach z logowania webowego.
            // Ręcznie (nie CheckPasswordSignInAsync), żeby ZACHOWAĆ kolejność: hasło sprawdzamy PRZED wymogiem
            // potwierdzenia e-maila — inaczej ujawnialibyśmy istnienie niepotwierdzonego konta bez znajomości hasła.
            if (await userManager.IsLockedOutAsync(user))
            {
                return ForbidWithError(Errors.InvalidGrant, localizer["Login_LockedOut"]);
            }

            if (!await userManager.CheckPasswordAsync(user, request.Password!))
            {
                // Zlicza nieudaną próbę i nakłada blokadę po przekroczeniu progu (Lockout w Program.cs).
                await userManager.AccessFailedAsync(user);
                return ForbidWithError(Errors.InvalidGrant, localizer["Login_InvalidCredentials"]);
            }

            // Udane hasło zeruje licznik nieudanych prób — tak samo jak robi to SignInManager przy logowaniu webowym.
            await userManager.ResetAccessFailedCountAsync(user);

            if (!await userManager.IsEmailConfirmedAsync(user))
            {
                // Ten sam wymóg co logowanie webowe (RequireConfirmedAccount, patrz Program.cs) — password
                // grant idzie bezpośrednio przez UserManager i pomija automatyczne egzekwowanie SignInManagera.
                return ForbidWithError(Errors.InvalidGrant, localizer["OAuth_CliEmailNotConfirmed"]);
            }

            if (await userManager.GetTwoFactorEnabledAsync(user))
            {
                return ForbidWithError(Errors.InvalidGrant, localizer["OAuth_CliTwoFactor"]);
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            principal.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user));
            principal.SetScopes(request.GetScopes());
            principal.SetResources(await ListResourcesAsync(principal.GetScopes()));

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            // Klient serwisowy serwera tożsamości (budgettracker-admin): token bez użytkownika, którym Identity woła
            // /api/admin API budżetu. Tylko ten klient ma zezwolenie na zakres admin (patrz OpenIddictSeeder), więc
            // ani SPA, ani bt-cli nie wyprosi takiego tokenu, a rola admin użytkownika go nie zastępuje.
            var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
                ?? throw new InvalidOperationException("Nieznana aplikacja kliencka.");

            var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, await applicationManager.GetClientIdAsync(application));
            identity.SetClaim(Claims.Name, await applicationManager.GetDisplayNameAsync(application));

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());
            principal.SetResources(await ListResourcesAsync(principal.GetScopes()));

            foreach (var claim in principal.Claims) claim.SetDestinations(Destinations.AccessToken);

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new NotImplementedException("Nieobsługiwany typ żądania tokenu.");
    }

    // Wywoływane z fetch() Bearerem, nie ciasteczkiem — bez jawnego schematu [Authorize] user
    // rozstrzygałby się przez domyślny schemat ciasteczkowy ASP.NET Identity i zawsze wychodził null.
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo")]
    public async Task<IActionResult> Userinfo()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge(authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = await userManager.GetUserIdAsync(user),
        };

        if (User.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = (await userManager.GetEmailAsync(user))!;
            claims[Claims.EmailVerified] = await userManager.IsEmailConfirmedAsync(user);
        }

        if (User.HasScope(Scopes.Profile))
        {
            claims[Claims.PreferredUsername] = (await userManager.GetUserNameAsync(user))!;
        }

        return Ok(claims);
    }

    [HttpGet("~/connect/logout")]
    public IActionResult Logout() => View("Logout");

    [ActionName(nameof(Logout))]
    [HttpPost("~/connect/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        await signInManager.SignOutAsync();

        // post_logout_redirect_uri jest w query stringu GET-a, który pokazał ekran potwierdzenia —
        // formularz w Logout.cshtml przepisuje go jako pole ukryte, tak samo jak ekran zgody
        // przepisuje oryginalne parametry /connect/authorize (OpenIddict czyta parametry POST-a
        // WYŁĄCZNIE z ciała, nie z query stringu).
        var request = HttpContext.GetOpenIddictServerRequest();
        var redirectUri = request?.PostLogoutRedirectUri;

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties
            {
                RedirectUri = string.IsNullOrEmpty(redirectUri) ? "/" : redirectUri,
            });
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Name or Claims.PreferredUsername:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Profile)) yield return Destinations.IdentityToken;
                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Email)) yield return Destinations.IdentityToken;
                yield break;

            case OAuthDefaults.TwoFactorEnabledClaimType or Claims.Role:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Profile)) yield return Destinations.IdentityToken;
                yield break;

            // ASP.NET Identity dokłada do principala własne roszczenia (SecurityStamp, a przede
            // wszystkim ClaimTypes.NameIdentifier/Name/Email jako pełne URI schematu XML) —
            // powielałyby dane, które już jawnie mapujemy wyżej pod krótkimi nazwami OpenIddict.
            default:
                yield break;
        }
    }
}
