using BudgetTracker.Identity.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Błędy ASP.NET Identity (walidacja hasła, duplikat e-maila itd.) domyślnie są po angielsku i bez
/// lokalizacji — tutaj biorą tekst z <c>SharedResource</c> pod kluczem <c>Identity_&lt;kod błędu&gt;</c>,
/// w języku bieżącego żądania.
/// </summary>
public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> localizer) : IdentityErrorDescriber
{
    private IdentityError Error(string code, params object[] args) =>
        new() { Code = code, Description = localizer[$"Identity_{code}", args] };

    public override IdentityError DefaultError() => Error(nameof(DefaultError));
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure));
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch));
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken));
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated));
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName));
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail));
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName));
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail));
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName));
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName));
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword));
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled));
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole));
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole));
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), length);
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), uniqueChars);
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric));
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit));
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower));
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper));
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed));
}
