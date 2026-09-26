using BudgetTracker.Identity.Services;
using BudgetTracker.Identity.Services.Emails;

namespace BudgetTracker.Identity.Tests;

public sealed class AccountEmailServiceTests
{
    private sealed class CapturingSender : IEmailSender
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
        {
            Sent.Add((to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private static (AccountEmailService Service, CapturingSender Sender) Create()
    {
        var sender = new CapturingSender();
        return (new AccountEmailService(new EmailTemplateRenderer(), sender, TestLocalizer.Create()), sender);
    }

    [Theory]
    [InlineData("pl", "Potwierdź adres e-mail — Wydatki.com", "Dziękujemy za założenie konta")]
    [InlineData("en", "Confirm your email address — Wydatki.com", "Thanks for creating an account")]
    public async Task Confirmation_mail_is_sent_in_the_language_of_the_request(string culture, string subject, string bodyFragment)
    {
        var (service, sender) = Create();

        await TestLocalizer.InCulture(culture, () =>
            service.SendConfirmationAsync("jan@example.com", "https://id.example.com/confirm?token=t", CancellationToken.None));

        var mail = Assert.Single(sender.Sent);
        Assert.Equal("jan@example.com", mail.To);
        Assert.Equal(subject, mail.Subject);
        Assert.Contains(bodyFragment, mail.Html);
        Assert.Contains($"<html lang=\"{culture}\">", mail.Html);
        Assert.Contains("href=\"https://id.example.com/confirm?token=t\"", mail.Html);
    }

    [Theory]
    [InlineData("pl", "Reset hasła — Wydatki.com", "odblokuje też konto")]
    [InlineData("en", "Reset your password — Wydatki.com", "also unlocks the account")]
    public async Task Reset_mail_mentions_that_the_reset_unlocks_the_account(string culture, string subject, string bodyFragment)
    {
        var (service, sender) = Create();

        await TestLocalizer.InCulture(culture, () =>
            service.SendPasswordResetAsync("jan@example.com", "https://id.example.com/reset", CancellationToken.None));

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(subject, mail.Subject);
        Assert.Contains(bodyFragment, mail.Html);
    }

    [Theory]
    [InlineData("pl", "Kod weryfikacyjny — Wydatki.com")]
    [InlineData("en", "Verification code — Wydatki.com")]
    public async Task Two_factor_mail_carries_the_code(string culture, string subject)
    {
        var (service, sender) = Create();

        await TestLocalizer.InCulture(culture, () =>
            service.SendTwoFactorCodeAsync("jan@example.com", "482913", CancellationToken.None));

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(subject, mail.Subject);
        Assert.Contains(">482913<", mail.Html);
    }
}
