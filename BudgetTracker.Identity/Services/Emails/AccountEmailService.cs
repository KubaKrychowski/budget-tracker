using System.Globalization;
using BudgetTracker.Identity.Resources;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Identity.Services.Emails;

public sealed class AccountEmailService(
    EmailTemplateRenderer renderer,
    IEmailSender emailSender,
    IStringLocalizer<SharedResource> localizer) : IAccountEmailService
{
    public Task SendConfirmationAsync(string to, string link, CancellationToken ct) =>
        SendLinkAsync(to, link, "Mail_Confirm", ct);

    public Task SendPasswordResetAsync(string to, string link, CancellationToken ct) =>
        SendLinkAsync(to, link, "Mail_Reset", ct);

    public Task SendTwoFactorCodeAsync(string to, string code, CancellationToken ct)
    {
        var values = BaseValues("Mail_Code");
        values["Body"] = localizer["Mail_Code_Body"];
        values["Code"] = code;
        values["CodeHint"] = localizer["Mail_Code_Hint"];

        return SendAsync(to, "Mail_Code", EmailTemplateRenderer.TwoFactorCodeTemplate, values, ct);
    }

    /// <summary>Potwierdzenie i reset mają ten sam kształt: nagłówek, treść, przycisk z linkiem i link zapasowy.</summary>
    private Task SendLinkAsync(string to, string link, string keyPrefix, CancellationToken ct)
    {
        var values = BaseValues(keyPrefix);
        values["Body"] = localizer[$"{keyPrefix}_Body"];
        values["ButtonLabel"] = localizer[$"{keyPrefix}_Button"];
        values["ButtonUrl"] = link;
        values["LinkFallback"] = localizer["Mail_LinkFallback"];

        return SendAsync(to, keyPrefix, EmailTemplateRenderer.LinkMessageTemplate, values, ct);
    }

    private Dictionary<string, string> BaseValues(string keyPrefix) => new()
    {
        ["Lang"] = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
        ["Title"] = localizer[$"{keyPrefix}_Subject"],
        ["AppName"] = localizer["App_Name"],
        ["Heading"] = localizer[$"{keyPrefix}_Heading"],
        ["Footer"] = localizer[$"{keyPrefix}_Footer"],
    };

    private Task SendAsync(
        string to, string keyPrefix, string template, Dictionary<string, string> values, CancellationToken ct) =>
        emailSender.SendAsync(to, localizer[$"{keyPrefix}_Subject"], renderer.Render(template, values), ct);
}
