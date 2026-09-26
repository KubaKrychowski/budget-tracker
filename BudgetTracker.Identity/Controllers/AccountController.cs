using BudgetTracker.Identity.Services.Api;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Resources;
using BudgetTracker.Identity.Services;
using BudgetTracker.Identity.Services.Emails;
using BudgetTracker.Identity.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using QRCoder;

namespace BudgetTracker.Identity.Controllers;

/// <summary>
/// Ekrany logowania/rejestracji/hasła/2FA (makiety w Figmie, plik „Logowanie (Identity)"). Renderowane
/// po stronie serwera (Razor) — front Angulara nie ma własnego UI logowania, tylko przekierowuje tutaj.
/// </summary>
public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAccountEmailService accountEmails,
    SpaOrigins spaOrigins,
    UserDeletionService userDeletion,
    AdminRoleService adminRoles,
    BetaInviteService invites,
    IUserDataClient userData,
    IStringLocalizer<SharedResource> localizer,
    ILogger<AccountController> logger) : Controller
{
    private const int RecoveryCodeCount = 10;

    /// <summary>Wystawca w aplikacji uwierzytelniającej — stały, żeby zmiana języka nie zdublowała wpisu w aplikacji.</summary>
    /// <remarks>
    /// ⚠️ Zmiana tej wartości NIE unieważnia już skonfigurowanego uwierzytelniania dwuskładnikowego: sekret
    /// zostaje ten sam, więc kody dalej działają. Zmienia się wyłącznie etykieta — konta zapisane wcześniej
    /// zachowują starą nazwę, bo aplikacja uwierzytelniająca trzyma ją u siebie i nikt jej stamtąd nie zmieni.
    /// Przy nazwie zmienionej w trakcie zamkniętej bety to kosmetyka; przy tysiącu kont byłby to rozjazd
    /// widoczny dla użytkowników i wymagałby ponownej konfiguracji.
    /// </remarks>
    private const string AuthenticatorIssuer = "Wydatki.com";

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(LoginWith2fa), new { model.ReturnUrl, model.RememberMe });
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, localizer["Login_LockedOut"]);
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            // RequireConfirmedAccount = true (patrz Program.cs) — PasswordSignInAsync odmawia, dopóki
            // e-mail nie zostanie potwierdzony, zamiast zwrócić Succeeded/RequiresTwoFactor.
            ModelState.AddModelError(string.Empty, localizer["Login_EmailNotConfirmed"]);
            model.ShowResendConfirmation = true;
            return View(model);
        }

        ModelState.AddModelError(string.Empty, localizer["Login_InvalidCredentials"]);
        return View(model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null) =>
        View(new RegisterViewModel { ReturnUrl = returnUrl, ClosedBeta = invites.ClosedBeta });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        model.ClosedBeta = invites.ClosedBeta;

        if (!model.AcceptTerms)
        {
            ModelState.AddModelError(nameof(model.AcceptTerms), localizer["Validation_TermsRequired"]);
        }

        if (!ModelState.IsValid) return View(model);

        // ⚠️ Sprawdzenie idzie PRZED CreateAsync, a nie po: konto niezaproszonego nie ma powstać nawet na chwilę.
        // Odmowa mówi wprost, że adresu nie ma na liście — to ujawnia, KOGO zaproszono, ale nie ujawnia, kto ma
        // konto, i tej granicy pilnuje reszta ekranów (ForgotPassword, ResendEmailConfirmation odpowiadają
        // zawsze tak samo). Zamknięta beta bez tej informacji byłaby nie do odróżnienia od zepsutego formularza:
        // zaproszony z literówką w adresie i niezaproszony widzieliby dokładnie to samo.
        if (!await invites.IsAllowedToRegisterAsync(model.Email, HttpContext.RequestAborted))
        {
            logger.LogInformation("Odrzucono rejestrację adresu spoza listy zaproszeń.");
            ModelState.AddModelError(nameof(model.Email), localizer["Register_NotInvited"]);
            return View(model);
        }

        var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // ⚠️ Magazyn plików zakładamy OD RAZU, ale niepowodzenie NIE przerywa rejestracji: konto już
        // istnieje, a odwracanie go z powodu chwilowej niedostępności API byłoby gorsze niż brak kontenera.
        // Brakujące magazyny zakłada się z panelu administracyjnego (akcja przy użytkowniku).
        try
        {
            await userData.CreateUserContainerAsync(user.Id, HttpContext.RequestAborted);
        }
        catch (UserDataServiceException ex)
        {
            logger.LogError(ex, "Nie udało się założyć magazynu plików dla konta {UserId}.", user.Id);
        }

        // Konto istnieje, ale RequireConfirmedAccount blokuje logowanie do kliknięcia w link —
        // zamiast SignInAsync wysyłamy potwierdzenie i pokazujemy ekran „sprawdź skrzynkę".
        await SendConfirmationEmailAsync(user, model.ReturnUrl);
        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    public IActionResult RegisterConfirmation() => View();

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string token, string? returnUrl = null)
    {
        var user = await userManager.FindByGuidAsync(userId);
        if (user is null) return RedirectToAction(nameof(Login));

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return View("ConfirmEmailFailed");
        }

        // Nie wracamy tu bezpośrednio do oryginalnego /connect/authorize (returnUrl) — link z maila
        // najczęściej otwiera się w INNEJ karcie niż ta, która zaczęła logowanie w SPA, a state/nonce
        // angular-auth-oidc-client trzyma we sessionStorage tamtej konkretnej karty. Dokańczanie tego
        // przepływu tutaj kończy się u klienta błędem "could not find matching config for state ...".
        // Zamiast tego logujemy w Identity (cookie, bezpiecznie międzykartowo) i odsyłamy do frontu,
        // który sam zainicjuje świeże logowanie — dzięki cookie przejdzie bez ponownego podawania hasła.
        // Adres z Admin:Emails dostaje rolę dopiero po potwierdzeniu — inaczej ktoś mógłby zarejestrować cudzy adres.
        await adminRoles.GrantIfConfiguredAsync(user);

        await signInManager.SignInAsync(user, isPersistent: false);
        return View("ConfirmEmailSuccess", new SpaLinkViewModel { SpaUrl = ResolveSpaOrigin(returnUrl) });
    }

    [HttpGet]
    public IActionResult ResendEmailConfirmation() => View(new ResendEmailConfirmationViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmailConfirmation(ResendEmailConfirmationViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);

        if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
        {
            await SendConfirmationEmailAsync(user, returnUrl: null);
        }
        else
        {
            logger.LogInformation(
                "Ponowna wysyłka potwierdzenia zażądana dla adresu {Email} (brak konta albo już potwierdzony).",
                model.Email);
        }

        // Ten sam ekran niezależnie od wyniku — jak przy ForgotPassword, żeby nie zdradzać,
        // które adresy są zarejestrowane.
        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPassword(string? email = null) =>
        View(new ForgotPasswordViewModel { Email = email ?? "" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);

        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.ActionLink(nameof(ResetPassword), values: new { email = model.Email, token })!;

            await accountEmails.SendPasswordResetAsync(model.Email, link, HttpContext.RequestAborted);
        }
        else
        {
            logger.LogInformation("Reset hasła zażądany dla nieistniejącego adresu {Email}.", model.Email);
        }

        // Zawsze ten sam ekran, niezależnie czy konto istnieje — inaczej formularz zdradzałby,
        // które adresy e-mail są zarejestrowane.
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (email is null || token is null) return RedirectToAction(nameof(Login));
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Ten sam ekran potwierdzenia co przy sukcesie — bez zdradzania istnienia konta.
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // Reset hasła to jedyny sposób odzyskania dostępu przed upływem blokady po 3 nieudanych
        // próbach (patrz Lockout w Program.cs) — ResetPasswordAsync sam z siebie NIE czyści blokady,
        // więc bez tego użytkownik miałby nowe hasło, ale nadal zablokowane konto.
        //
        // ⚠️ Wyjątek: blokada na stałe (koniec = MaxValue) to nie anty-bruteforce, tylko pierwszy krok usuwania konta
        // (patrz UserDeletionService). Zdjęcie jej przez reset hasła pozwoliłoby użytkownikowi wrócić na konto, które
        // administrator właśnie kasuje, więc tę blokadę zostawiamy.
        if (await userManager.GetLockoutEndDateAsync(user) != DateTimeOffset.MaxValue)
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
        }

        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    [HttpGet]
    public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl = null)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        return View(new LoginWith2faViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model, bool rememberMe)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            NormalizeCode(model.TwoFactorCode), rememberMe, model.RememberMachine);

        if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);

        ModelState.AddModelError(string.Empty,
            localizer[result.IsLockedOut ? "TwoFactor_LockedOut" : "TwoFactor_InvalidCode"]);
        return View(model);
    }

    /// <summary>Alternatywa dla TOTP z aplikacji uwierzytelniającej — kod wysyłany e-mailem na żądanie.</summary>
    [HttpGet]
    public async Task<IActionResult> LoginWith2faEmail(bool rememberMe, string? returnUrl = null)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        await SendTwoFactorEmailCodeAsync(user);
        return View(new LoginWith2faEmailViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2faEmail(LoginWith2faEmailViewModel model, bool rememberMe)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        var result = await signInManager.TwoFactorSignInAsync(
            TokenOptions.DefaultEmailProvider, NormalizeCode(model.TwoFactorCode), rememberMe, model.RememberMachine);

        if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);

        ModelState.AddModelError(string.Empty,
            localizer[result.IsLockedOut ? "TwoFactor_LockedOut" : "TwoFactor_InvalidCode"]);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LoginWithRecoveryCode(string? returnUrl = null)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        return View(new LoginWithRecoveryCodeViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWithRecoveryCode(LoginWithRecoveryCodeViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(model.RecoveryCode.Replace(" ", ""));

        if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);

        ModelState.AddModelError(string.Empty,
            localizer[result.IsLockedOut ? "TwoFactor_LockedOut" : "TwoFactor_InvalidRecoveryCode"]);
        return View(model);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> EnableAuthenticator(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await userManager.GetEmailAsync(user);
        var authenticatorUri = BuildAuthenticatorUri(email!, key!);

        return View(new EnableAuthenticatorViewModel
        {
            SharedKey = FormatKey(key!),
            AuthenticatorUri = authenticatorUri,
            ReturnUrl = ValidateReturnUrl(returnUrl),
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();

        if (!ModelState.IsValid) return await EnableAuthenticatorViewAsync(user, model);

        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(model.Code));

        if (!isValid)
        {
            ModelState.AddModelError(nameof(model.Code), localizer["TwoFactor_InvalidCode"]);
            return await EnableAuthenticatorViewAsync(user, model);
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);

        TempData[TempDataKeys.RecoveryCodes] = recoveryCodes?.ToArray() ?? [];
        TempData[TempDataKeys.RecoveryCodesReturnUrl] = ValidateReturnUrl(model.ReturnUrl);
        return RedirectToAction(nameof(ShowRecoveryCodes));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ShowRecoveryCodes()
    {
        if (TempData[TempDataKeys.RecoveryCodes] is not string[] codes || codes.Length == 0)
        {
            return RedirectToDefault();
        }

        return View(new RecoveryCodesViewModel
        {
            Codes = codes,
            SpaUrl = TempData[TempDataKeys.RecoveryCodesReturnUrl] as string ?? spaOrigins.Default,
        });
    }

    /// <summary>
    /// Wyłączenie 2FA, wywoływane z linku na ekranie „Konto i bezpieczeństwo" w Angularze — stąd
    /// GET pokazuje tylko potwierdzenie (bez efektu ubocznego), a POST robi właściwą zmianę.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> DisableTwoFactor(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();
        if (!await userManager.GetTwoFactorEnabledAsync(user)) return RedirectToLocalOrSpaOrigin(returnUrl);

        return View(new ReturnUrlViewModel { ReturnUrl = ValidateReturnUrl(returnUrl) });
    }

    [Authorize]
    [HttpPost]
    [ActionName(nameof(DisableTwoFactor))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableTwoFactorConfirmed(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();

        await userManager.SetTwoFactorEnabledAsync(user, false);
        // Ponowne włączenie ma wymagać świeżego zeskanowania QR, nie akceptowania starego sekretu
        // TOTP — inaczej ktoś, kto przechwycił poprzedni klucz, mógłby po cichu wrócić do gry.
        await userManager.ResetAuthenticatorKeyAsync(user);

        return RedirectToLocalOrSpaOrigin(returnUrl);
    }

    /// <summary>
    /// Wygenerowanie nowego kompletu kodów zapasowych na żądanie (np. bo poprzednie się skończyły) —
    /// w odróżnieniu od <see cref="EnableAuthenticator(EnableAuthenticatorViewModel)"/>, który pokazuje
    /// je tylko raz przy pierwszym włączeniu 2FA.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> RegenerateRecoveryCodes(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();
        if (!await userManager.GetTwoFactorEnabledAsync(user)) return RedirectToLocalOrSpaOrigin(returnUrl);

        return View(new ReturnUrlViewModel { ReturnUrl = ValidateReturnUrl(returnUrl) });
    }

    [Authorize]
    [HttpPost]
    [ActionName(nameof(RegenerateRecoveryCodes))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateRecoveryCodesConfirmed(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);

        TempData[TempDataKeys.RecoveryCodes] = recoveryCodes?.ToArray() ?? [];
        TempData[TempDataKeys.RecoveryCodesReturnUrl] = ValidateReturnUrl(returnUrl);
        return RedirectToAction(nameof(ShowRecoveryCodes));
    }

    /// <summary>
    /// „Usuń moje konto" — wołane z linku w Ustawieniach (Angular). GET tylko pokazuje ostrzeżenie; kasuje dopiero POST
    /// po podaniu hasła (i kodu 2FA, jeśli jest włączone), więc samo otwarcie linku niczego nie zmienia.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> DeleteAccount(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();
        return View(await BuildDeleteAccountModelAsync(user, returnUrl));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();
        model.Email = user.Email ?? "";
        model.RequiresTwoFactor = await userManager.GetTwoFactorEnabledAsync(user);
        model.ReturnUrl = ValidateReturnUrl(model.ReturnUrl);

        if (!ModelState.IsValid) return View(model);

        if (!await userManager.CheckPasswordAsync(user, model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), localizer["DeleteAccount_WrongPassword"]);
            return View(model);
        }

        if (model.RequiresTwoFactor && !await userManager.VerifyTwoFactorTokenAsync(
                user, userManager.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(model.TwoFactorCode ?? "")))
        {
            ModelState.AddModelError(nameof(model.TwoFactorCode), localizer["TwoFactor_InvalidCode"]);
            return View(model);
        }

        var outcome = await userDeletion.DeleteOwnAccountAsync(user.Id, HttpContext.RequestAborted);
        switch (outcome)
        {
            case UserDeletionOutcome.Deleted or UserDeletionOutcome.NotFound:
                await signInManager.SignOutAsync();
                return RedirectToAction(nameof(AccountDeleted));

            case UserDeletionOutcome.LastAdmin:
                ModelState.AddModelError(string.Empty, localizer["DeleteAccount_LastAdmin"]);
                return View(model);

            default:
                ModelState.AddModelError(string.Empty, localizer["DeleteAccount_DataUnavailable"]);
                return View(model);
        }
    }

    [HttpGet]
    public IActionResult AccountDeleted() => View(new SpaLinkViewModel { SpaUrl = spaOrigins.Default });

    private async Task<DeleteAccountViewModel> BuildDeleteAccountModelAsync(ApplicationUser user, string? returnUrl) => new()
    {
        Email = user.Email ?? "",
        RequiresTwoFactor = await userManager.GetTwoFactorEnabledAsync(user),
        ReturnUrl = ValidateReturnUrl(returnUrl),
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToDefault();
    }

    [HttpGet]
    public IActionResult AccessDenied() => View(new SpaLinkViewModel { SpaUrl = spaOrigins.Default });

    /// <summary>Kod QR renderowany jako obraz PNG osadzony data-URI — bez wywołania zewnętrznego API.</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> AuthenticatorQrCode()
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException();
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key)) return NotFound();

        var uri = BuildAuthenticatorUri((await userManager.GetEmailAsync(user))!, key);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);

        return File(png, "image/png");
    }

    /// <summary>Ponownie pokazuje formularz włączania 2FA — z kluczem i adresem QR, których nie ma w odesłanym modelu.</summary>
    private async Task<IActionResult> EnableAuthenticatorViewAsync(ApplicationUser user, EnableAuthenticatorViewModel model)
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        model.SharedKey = FormatKey(key!);
        model.AuthenticatorUri = BuildAuthenticatorUri((await userManager.GetEmailAsync(user))!, key!);
        return View(model);
    }

    private static string NormalizeCode(string code) => code.Replace(" ", "").Replace("-", "");

    private static string BuildAuthenticatorUri(string email, string unformattedKey)
    {
        var issuer = Uri.EscapeDataString(AuthenticatorIssuer);
        return $"otpauth://totp/{issuer}:{Uri.EscapeDataString(email)}?secret={unformattedKey}&issuer={issuer}&digits=6";
    }

    private static string FormatKey(string unformattedKey)
    {
        var chunks = new List<string>();
        var remaining = unformattedKey;
        while (remaining.Length > 0)
        {
            var chunkSize = Math.Min(4, remaining.Length);
            chunks.Add(remaining[..chunkSize]);
            remaining = remaining[chunkSize..];
        }

        return string.Join(' ', chunks);
    }

    /// <summary>
    /// Dokąd po zalogowaniu bez własnego adresu powrotu: do zarejestrowanego frontu, a gdy żaden nie jest
    /// skonfigurowany — z powrotem na ekran logowania.
    /// </summary>
    private IActionResult RedirectToDefault() =>
        spaOrigins.Default is { } origin ? Redirect(origin) : RedirectToAction(nameof(Login));

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToDefault();

    /// <summary>
    /// Jak <see cref="RedirectToLocal"/>, ale dopuszcza też pełny URL do SPA (np. konkretną podstronę
    /// ekranu „Ustawienia" w Angularze) — używane przez akcje wywoływane z linków w Angularze
    /// (<see cref="DisableTwoFactor(string?)"/>, <see cref="RegenerateRecoveryCodes(string?)"/>).
    /// Bez sprawdzenia originu wobec skonfigurowanych klientów SPA byłoby to open redirect.
    /// </summary>
    private IActionResult RedirectToLocalOrSpaOrigin(string? returnUrl) =>
        ValidateReturnUrl(returnUrl) is { } validated ? Redirect(validated) : RedirectToDefault();

    private string? ValidateReturnUrl(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl)) return returnUrl;

        return Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
               && spaOrigins.IsAllowed(uri.GetLeftPart(UriPartial.Authority))
            ? returnUrl
            : null;
    }

    /// <summary>Generuje token potwierdzający i wysyła link na adres konta.</summary>
    private async Task SendConfirmationEmailAsync(ApplicationUser user, string? returnUrl)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = Url.ActionLink(nameof(ConfirmEmail), values: new { userId = user.Id, token, returnUrl })!;

        await accountEmails.SendConfirmationAsync(user.Email!, link, HttpContext.RequestAborted);
    }

    /// <summary>
    /// EmailTokenProvider (patrz AddDefaultTokenProviders w Program.cs) nie wymaga osobnego "włączenia" tak
    /// jak TOTP — działa od razu, gdy tylko TwoFactorEnabled=true, bo liczy token z SecurityStamp usera.
    /// </summary>
    private async Task SendTwoFactorEmailCodeAsync(ApplicationUser user)
    {
        var code = await userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
        await accountEmails.SendTwoFactorCodeAsync(user.Email!, code, HttpContext.RequestAborted);
    }

    /// <summary>
    /// Origin SPA, do którego wraca przycisk po potwierdzeniu maila: ten z <c>redirect_uri</c> zaszytego
    /// w returnUrl (dev 4200 / demo 4310), o ile to zarejestrowany klient — inaczej domyślny origin.
    /// returnUrl to zwykle oryginalny query string "/connect/authorize?...&amp;redirect_uri=...".
    /// </summary>
    private string? ResolveSpaOrigin(string? returnUrl) =>
        ExtractOrigin(returnUrl) is { } origin && spaOrigins.IsAllowed(origin) ? origin : spaOrigins.Default;

    private static string? ExtractOrigin(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)) return null;

        var queryStart = returnUrl.IndexOf('?');
        if (queryStart < 0) return null;

        var query = QueryHelpers.ParseQuery(returnUrl[queryStart..]);
        if (!query.TryGetValue(OpenIddictConstants.Parameters.RedirectUri, out var redirectUri)) return null;

        return Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
    }
}
