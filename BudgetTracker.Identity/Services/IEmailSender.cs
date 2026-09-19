using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using BudgetTracker.Identity.Options;

namespace BudgetTracker.Identity.Services;

/// <summary>
/// Wysyłka maili transakcyjnych: potwierdzenie konta, reset hasła, kod weryfikacji dwuetapowej.
/// Jedyna implementacja (<see cref="SmtpEmailSender"/>) mówi zwykłym SMTP — działa i z MailHogiem
/// (Development, bez uwierzytelnienia/TLS), i z SMTP z Azure (produkcja/staging, STARTTLS + login).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct);
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

        using var client = new SmtpClient();

        var socketOptions = smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, ct);

        if (!string.IsNullOrEmpty(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? "", ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);

        logger.LogInformation("Wysłano e-mail do {To}: {Subject}", to, subject);
    }
}
