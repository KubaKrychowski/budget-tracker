using Azure;
using Azure.Communication.Email;
using BudgetTracker.Identity.Options;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Identity.Services;

/// <summary>
/// Wysyłka maili transakcyjnych przez Azure Communication Services (SDK).
/// </summary>
/// <remarks>
/// Druga implementacja <see cref="IEmailSender"/> obok <see cref="SmtpEmailSender"/>. Wybór należy do
/// konfiguracji, nie do kodu: wypełniona sekcja <c>Acs:Email</c> wygrywa, pusta zostawia SMTP (MailHog
/// w Development). Patrz <see cref="AcsEmailOptions"/>.
/// </remarks>
public sealed class AcsEmailSender : IEmailSender
{
    private readonly EmailClient client;
    private readonly string senderAddress;
    private readonly ILogger<AcsEmailSender> logger;

    public AcsEmailSender(IOptions<AcsEmailOptions> options, ILogger<AcsEmailSender> logger)
    {
        var acs = options.Value;

        if (!acs.IsConfigured)
        {
            throw new InvalidOperationException(
                $"Sekcja {AcsEmailOptions.SectionName} jest niekompletna — potrzebne ConnectionString i SenderAddress.");
        }

        client = new EmailClient(acs.ConnectionString);
        senderAddress = acs.SenderAddress!;
        this.logger = logger;
    }

    /// <remarks>
    /// ⚠️ <see cref="WaitUntil.Started"/>, nie <c>Completed</c>. Wysyłka maila jest w ACS operacją
    /// długotrwałą, więc czekanie na jej zakończenie zatrzymałoby żądanie HTTP rejestracji na czas
    /// doręczenia. Zachowanie jest przez to takie samo jak przy SMTP: błędy odrzucenia (zły nadawca, złe
    /// dane dostępowe) wychodzą od razu, a niepowodzenie doręczenia PÓŹNIEJ jest niewidoczne dla aplikacji.
    /// </remarks>
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var message = new EmailMessage(
            senderAddress: senderAddress,
            recipientAddress: to,
            content: new EmailContent(subject) { Html = htmlBody });

        await client.SendAsync(WaitUntil.Started, message, ct);

        logger.LogInformation("Wysłano e-mail do {To}: {Subject}", to, subject);
    }
}
