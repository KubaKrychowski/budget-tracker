using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Identity.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Podaj adres e-mail.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Podaj hasło.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Podaj adres e-mail.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Podaj hasło.")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Hasło musi mieć co najmniej {2} znaków.", MinimumLength = 8)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Powtórz hasło.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Hasła nie są identyczne.")]
    public string ConfirmPassword { get; set; } = "";

    public bool AcceptTerms { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Podaj adres e-mail.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    public string Email { get; set; } = "";
}

public sealed class ResendEmailConfirmationViewModel
{
    [Required(ErrorMessage = "Podaj adres e-mail.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    public string Email { get; set; } = "";
}

public sealed class ConfirmEmailSuccessViewModel
{
    /// <summary>Origin frontu (np. "http://localhost:4200") do przycisku powrotu, jeśli udało się go ustalić.</summary>
    public string? SpaUrl { get; set; }
}

public sealed class ResetPasswordViewModel
{
    [Required]
    public string Email { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Podaj nowe hasło.")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Hasło musi mieć co najmniej {2} znaków.", MinimumLength = 8)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Powtórz nowe hasło.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Hasła nie są identyczne.")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class LoginWith2faViewModel
{
    [Required(ErrorMessage = "Podaj kod weryfikacyjny.")]
    [StringLength(7, ErrorMessage = "Kod ma nieprawidłową długość.", MinimumLength = 6)]
    public string TwoFactorCode { get; set; } = "";

    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class LoginWith2faEmailViewModel
{
    [Required(ErrorMessage = "Podaj kod weryfikacyjny.")]
    [StringLength(7, ErrorMessage = "Kod ma nieprawidłową długość.", MinimumLength = 6)]
    public string TwoFactorCode { get; set; } = "";

    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class LoginWithRecoveryCodeViewModel
{
    [Required(ErrorMessage = "Podaj kod zapasowy.")]
    public string RecoveryCode { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class EnableAuthenticatorViewModel
{
    public string SharedKey { get; set; } = "";

    public string AuthenticatorUri { get; set; } = "";

    [Required(ErrorMessage = "Podaj kod z aplikacji.")]
    [StringLength(7, ErrorMessage = "Kod ma nieprawidłową długość.", MinimumLength = 6)]
    public string Code { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class RecoveryCodesViewModel
{
    public required IReadOnlyList<string> Codes { get; init; }

    /// <summary>Origin frontu do przycisku powrotu — ustawiony tylko, gdy wygenerowano z linku z Angulara.</summary>
    public string? SpaUrl { get; init; }
}

/// <summary>Ekrany potwierdzenia (wyłącz 2FA, wygeneruj nowe kody zapasowe) wywoływane z linku w Angularze.</summary>
public sealed class ReturnUrlViewModel
{
    public string? ReturnUrl { get; set; }
}
