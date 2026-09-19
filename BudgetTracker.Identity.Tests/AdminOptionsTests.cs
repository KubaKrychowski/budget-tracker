using BudgetTracker.Identity.Options;

namespace BudgetTracker.Identity.Tests;

public sealed class AdminOptionsTests
{
    private static AdminOptions With(params string[] emails) => new() { Emails = emails };

    [Theory]
    [InlineData("jan@example.com", true)]
    [InlineData("JAN@Example.COM", true)]
    [InlineData("  jan@example.com  ", true)]
    [InlineData("anna@example.com", false)]
    // Adres zawierający adres admina to inne konto — dopasowanie musi być dokładne, nie „zawiera".
    [InlineData("xjan@example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_addresses_on_the_list_are_admins_regardless_of_case_and_padding(string? email, bool expected)
    {
        Assert.Equal(expected, With("jan@example.com").IsConfiguredAdmin(email));
    }

    [Fact]
    public void With_no_configured_addresses_nobody_is_an_admin()
    {
        Assert.False(new AdminOptions().IsConfiguredAdmin("jan@example.com"));
    }

    [Theory]
    [InlineData("https://localhost:7133", true)]
    [InlineData("https://api.example.com/", true)]
    // http odrzucamy: serwer tożsamości wysyła tam token serwisowy, który wolno ujawnić tylko przez TLS.
    [InlineData("http://localhost:5031", false)]
    [InlineData("", false)]
    [InlineData("localhost:7133", false)]
    public void The_api_address_must_be_an_absolute_https_url(string url, bool valid)
    {
        Assert.Equal(valid, new ApiOptions { BaseUrl = url }.HasValidBaseUrl);
    }
}
