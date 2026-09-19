namespace BudgetTracker.Identity.Services.Emails;

/// <summary>
/// Maile transakcyjne konta w języku bieżącego żądania: potwierdzenie adresu, reset hasła, kod 2FA.
/// Treść i temat pochodzą z <c>SharedResource</c> (klucze <c>Mail_*</c>), wygląd z szablonów w <c>Emails/</c>.
/// </summary>
public interface IAccountEmailService
{
    Task SendConfirmationAsync(string to, string link, CancellationToken ct);

    Task SendPasswordResetAsync(string to, string link, CancellationToken ct);

    Task SendTwoFactorCodeAsync(string to, string code, CancellationToken ct);
}
