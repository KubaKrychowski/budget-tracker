using BudgetTracker.Identity.Services.Emails;

namespace BudgetTracker.Identity.Tests;

public sealed class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer renderer = new();

    private static Dictionary<string, string> LinkValues(string url = "https://id.example.com/Account/ConfirmEmail?userId=1&token=abc") => new()
    {
        ["Lang"] = "pl",
        ["Title"] = "Temat",
        ["AppName"] = "Budżet tracker",
        ["Heading"] = "Nagłówek",
        ["Body"] = "Treść",
        ["ButtonLabel"] = "Kliknij",
        ["ButtonUrl"] = url,
        ["LinkFallback"] = "Skopiuj adres:",
        ["Footer"] = "Stopka",
    };

    [Fact]
    public void Link_mail_is_a_complete_html_page_with_the_stylesheet_inlined_into_head()
    {
        var html = renderer.Render(EmailTemplateRenderer.LinkMessageTemplate, LinkValues());

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"pl\">", html);
        Assert.Contains("<style>", html);
        // Arkusz jest wstawiany do <style>, nie linkowany: klienci poczty ignorują zewnętrzne CSS.
        Assert.Contains(".button-cell", html);
        Assert.Contains("Nagłówek", html);
        Assert.Contains("Stopka", html);
    }

    [Fact]
    public void Link_mail_has_no_unresolved_placeholders_left()
    {
        var html = renderer.Render(EmailTemplateRenderer.LinkMessageTemplate, LinkValues());

        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void Values_are_html_encoded_so_they_cannot_inject_markup()
    {
        var values = LinkValues();
        values["Body"] = "<script>alert(1)</script>";
        values["ButtonUrl"] = "https://id.example.com/x?a=1&b=\"2\"";

        var html = renderer.Render(EmailTemplateRenderer.LinkMessageTemplate, values);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        // & w adresie musi być zapisane jako &amp; w atrybucie href, a cudzysłów nie może go zamknąć.
        Assert.Contains("href=\"https://id.example.com/x?a=1&amp;b=&quot;2&quot;\"", html);
    }

    [Fact]
    public void Code_mail_shows_the_code_and_hint()
    {
        var values = new Dictionary<string, string>
        {
            ["Lang"] = "en", ["Title"] = "T", ["AppName"] = "A", ["Heading"] = "Verification code",
            ["Body"] = "Use this code", ["Code"] = "123456", ["CodeHint"] = "Enter it right away", ["Footer"] = "F",
        };

        var html = renderer.Render(EmailTemplateRenderer.TwoFactorCodeTemplate, values);

        Assert.Contains(">123456<", html);
        Assert.Contains("Enter it right away", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void Missing_value_fails_loudly_instead_of_leaving_a_placeholder_in_the_mail()
    {
        var values = LinkValues();
        values.Remove("ButtonLabel");

        var ex = Assert.Throws<InvalidOperationException>(
            () => renderer.Render(EmailTemplateRenderer.LinkMessageTemplate, values));

        Assert.Contains("ButtonLabel", ex.Message);
    }
}
