using System.ComponentModel.DataAnnotations;
using BudgetTracker.Identity.Infrastructure;

namespace BudgetTracker.Identity.Models;

/// <remarks>
/// <c>ErrorMessage</c> w atrybutach walidacji to KLUCZ zasobu (<c>SharedResource</c>), nie tekst —
/// <c>AddDataAnnotationsLocalization</c> w <c>Program.cs</c> podmienia go na tłumaczenie w języku żądania.
/// </remarks>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Validation_EmailRequired")]
    [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Validation_PasswordRequired")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>Ustawiane po odmowie z powodu niepotwierdzonego e-maila — pokazuje link do ponownej wysyłki.</summary>
    public bool ShowResendConfirmation { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Validation_EmailRequired")]
    [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Validation_PasswordRequired")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Validation_PasswordMinLength", MinimumLength = PasswordPolicy.MinLength)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Validation_ConfirmPasswordRequired")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Validation_PasswordsDiffer")]
    public string ConfirmPassword { get; set; } = "";

    public bool AcceptTerms { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Czy serwer wpuszcza tylko zaproszonych. Ekran mówi o tym OD RAZU, a nie dopiero po odesłaniu formularza:
    /// wypełnianie hasła i regulaminu po to, żeby usłyszeć „nie ma cię na liście", jest kosztem, który da się
    /// zdjąć jednym zdaniem na górze.
    /// </summary>
    public bool ClosedBeta { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Validation_EmailRequired")]
    [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
    public string Email { get; set; } = "";
}

public sealed class ResendEmailConfirmationViewModel
{
    [Required(ErrorMessage = "Validation_EmailRequired")]
    [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
    public string Email { get; set; } = "";
}

/// <summary>Ekrany, które kończą się przyciskiem powrotu do aplikacji (potwierdzony e-mail, brak dostępu).</summary>
public sealed class SpaLinkViewModel
{
    /// <summary>Origin frontu (np. "https://localhost:4200") do przycisku powrotu, jeśli udało się go ustalić.</summary>
    public string? SpaUrl { get; init; }
}

public sealed class ResetPasswordViewModel
{
    [Required]
    public string Email { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Validation_NewPasswordRequired")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Validation_PasswordMinLength", MinimumLength = PasswordPolicy.MinLength)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Validation_ConfirmNewPasswordRequired")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Validation_PasswordsDiffer")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class LoginWith2faViewModel
{
    [Required(ErrorMessage = "Validation_CodeRequired")]
    [StringLength(7, ErrorMessage = "Validation_CodeLength", MinimumLength = 6)]
    public string TwoFactorCode { get; set; } = "";

    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class LoginWith2faEmailViewModel
{
    [Required(ErrorMessage = "Validation_CodeRequired")]
    [StringLength(7, ErrorMessage = "Validation_CodeLength", MinimumLength = 6)]
    public string TwoFactorCode { get; set; } = "";

    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class LoginWithRecoveryCodeViewModel
{
    [Required(ErrorMessage = "Validation_RecoveryCodeRequired")]
    public string RecoveryCode { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class EnableAuthenticatorViewModel
{
    public string SharedKey { get; set; } = "";

    public string AuthenticatorUri { get; set; } = "";

    [Required(ErrorMessage = "Validation_AuthenticatorCodeRequired")]
    [StringLength(7, ErrorMessage = "Validation_CodeLength", MinimumLength = 6)]
    public string Code { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class RecoveryCodesViewModel
{
    public required IReadOnlyList<string> Codes { get; init; }

    /// <summary>Origin frontu do przycisku powrotu — z linku w Angularze albo domyślny origin SPA.</summary>
    public string? SpaUrl { get; init; }
}

/// <summary>Ekrany potwierdzenia (wyłącz 2FA, wygeneruj nowe kody zapasowe) wywoływane z linku w Angularze.</summary>
public sealed class ReturnUrlViewModel
{
    public string? ReturnUrl { get; set; }
}

/// <summary>„Usuń moje konto": potwierdzenie hasłem i, gdy konto ma 2FA, kodem z aplikacji uwierzytelniającej.</summary>
public sealed class DeleteAccountViewModel
{
    public string Email { get; set; } = "";

    public bool RequiresTwoFactor { get; set; }

    [Required(ErrorMessage = "Validation_PasswordRequired")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? TwoFactorCode { get; set; }

    public string? ReturnUrl { get; set; }
}
