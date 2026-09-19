namespace BudgetTracker.Identity.Infrastructure;

/// <summary>Nazwy ról ASP.NET Identity. Rola trafia też do tokenów jako roszczenie <c>role</c>.</summary>
public static class IdentityRoles
{
    /// <summary>
    /// Administrator: widzi ekrany zarządzania użytkownikami. Nadawana adresom z konfiguracji <c>Admin:Emails</c>
    /// (patrz <c>AdminOptions</c>), nigdy przez rejestrację ani ekran w aplikacji.
    /// </summary>
    public const string Admin = "admin";
}
