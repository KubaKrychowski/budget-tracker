using BudgetTracker.Identity.Options;
using BudgetTracker.Identity.Services;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace BudgetTracker.Identity.Tests;

public sealed class SpaOriginsTests
{
    private static SpaOrigins Create(params string[] redirectUris) =>
        new(MsOptions.Create(new SpaClientOptions { RedirectUris = redirectUris }));

    [Fact]
    public void Origins_are_derived_from_the_registered_redirect_uris_without_duplicates()
    {
        var origins = Create(
            "http://localhost:4200/auth-callback", "http://localhost:4200/silent-renew.html", "http://localhost:4310/auth-callback");

        Assert.Equal(["http://localhost:4200", "http://localhost:4310"], origins.All);
        Assert.Equal("http://localhost:4200", origins.Default);
    }

    [Theory]
    [InlineData("http://localhost:4200", true)]
    [InlineData("HTTP://LOCALHOST:4200", true)]
    [InlineData("http://evil.example.com", false)]
    // Ten sam host, inny port albo protokół to inny origin — wpuszczenie go byłoby dziurą.
    [InlineData("http://localhost:9999", false)]
    [InlineData("https://localhost:4200", false)]
    public void Only_registered_origins_are_allowed(string origin, bool allowed)
    {
        Assert.Equal(allowed, Create("http://localhost:4200/auth-callback").IsAllowed(origin));
    }

    [Fact]
    public void Without_any_registered_client_there_is_no_default_origin_and_nothing_is_allowed()
    {
        var origins = Create();

        Assert.Null(origins.Default);
        Assert.False(origins.IsAllowed("http://localhost:4200"));
    }
}
