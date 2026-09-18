using Microsoft.AspNetCore.Identity;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Błędy ASP.NET Identity (walidacja hasła, duplikat e-maila itd.) domyślnie są po angielsku —
/// podmieniamy je na polskie, żeby pasowały do reszty ekranów logowania/rejestracji.
/// </summary>
public sealed class PolishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new()
    {
        Code = nameof(DefaultError),
        Description = "Wystąpił nieoczekiwany błąd.",
    };

    public override IdentityError ConcurrencyFailure() => new()
    {
        Code = nameof(ConcurrencyFailure),
        Description = "Dane zostały zmienione przez inną operację. Spróbuj ponownie.",
    };

    public override IdentityError PasswordMismatch() => new()
    {
        Code = nameof(PasswordMismatch),
        Description = "Nieprawidłowe hasło.",
    };

    public override IdentityError InvalidToken() => new()
    {
        Code = nameof(InvalidToken),
        Description = "Token jest nieprawidłowy albo wygasł.",
    };

    public override IdentityError LoginAlreadyAssociated() => new()
    {
        Code = nameof(LoginAlreadyAssociated),
        Description = "Konto z tym loginem już istnieje.",
    };

    public override IdentityError InvalidUserName(string? userName) => new()
    {
        Code = nameof(InvalidUserName),
        Description = "Nazwa użytkownika jest nieprawidłowa.",
    };

    public override IdentityError InvalidEmail(string? email) => new()
    {
        Code = nameof(InvalidEmail),
        Description = "Adres e-mail jest nieprawidłowy.",
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = "Konto z tym adresem e-mail już istnieje.",
    };

    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = "Konto z tym adresem e-mail już istnieje.",
    };

    public override IdentityError InvalidRoleName(string? role) => new()
    {
        Code = nameof(InvalidRoleName),
        Description = "Nazwa roli jest nieprawidłowa.",
    };

    public override IdentityError DuplicateRoleName(string role) => new()
    {
        Code = nameof(DuplicateRoleName),
        Description = "Taka rola już istnieje.",
    };

    public override IdentityError UserAlreadyHasPassword() => new()
    {
        Code = nameof(UserAlreadyHasPassword),
        Description = "Konto ma już ustawione hasło.",
    };

    public override IdentityError UserLockoutNotEnabled() => new()
    {
        Code = nameof(UserLockoutNotEnabled),
        Description = "Blokada konta nie jest włączona dla tego użytkownika.",
    };

    public override IdentityError UserAlreadyInRole(string role) => new()
    {
        Code = nameof(UserAlreadyInRole),
        Description = "Użytkownik ma już tę rolę.",
    };

    public override IdentityError UserNotInRole(string role) => new()
    {
        Code = nameof(UserNotInRole),
        Description = "Użytkownik nie ma tej roli.",
    };

    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"Hasło musi mieć co najmniej {length} znaków.",
    };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    {
        Code = nameof(PasswordRequiresUniqueChars),
        Description = $"Hasło musi zawierać co najmniej {uniqueChars} różnych znaków.",
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "Hasło musi zawierać co najmniej jeden znak specjalny.",
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "Hasło musi zawierać co najmniej jedną cyfrę.",
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "Hasło musi zawierać co najmniej jedną małą literę.",
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "Hasło musi zawierać co najmniej jedną wielką literę.",
    };

    public override IdentityError RecoveryCodeRedemptionFailed() => new()
    {
        Code = nameof(RecoveryCodeRedemptionFailed),
        Description = "Nie udało się użyć kodu zapasowego.",
    };
}
