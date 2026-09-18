namespace BudgetTracker.Identity.Infrastructure;

/// <summary>Klucze <c>TempData</c> przekazujące dane między akcją a przekierowaniem, które ją kończy.</summary>
public static class TempDataKeys
{
    /// <summary>Świeżo wygenerowane kody zapasowe 2FA — pokazywane raz, potem znikają.</summary>
    public const string RecoveryCodes = "RecoveryCodes";

    /// <summary>Adres SPA, do którego prowadzi przycisk powrotu na ekranie kodów zapasowych.</summary>
    public const string RecoveryCodesReturnUrl = "RecoveryCodesReturnUrl";
}
