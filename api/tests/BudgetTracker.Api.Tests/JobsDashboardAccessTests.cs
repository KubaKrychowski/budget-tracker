using BudgetTracker.Api.Infrastructure.Jobs;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Bilet i ciasteczko do panelu Hangfire (<see cref="JobsDashboardAccess"/>). Bez bazy: Data Protection w pamięci.
///
/// ⚠️ To jedyna bariera między internetem a panelem, który potrafi kasować zadania i pokazuje identyfikatory
/// użytkowników, więc testujemy odmowy, nie tylko ścieżkę szczęśliwą.
/// </summary>
public sealed class JobsDashboardAccessTests
{
    private static JobsDashboardAccess NewAccess(
        IDataProtectionProvider? provider = null, TimeSpan? ticket = null, TimeSpan? cookie = null) =>
        new(provider ?? new EphemeralDataProtectionProvider())
        {
            TicketLifetime = ticket ?? TimeSpan.FromSeconds(60),
            CookieLifetime = cookie ?? TimeSpan.FromMinutes(30),
        };

    [Fact]
    public void A_fresh_ticket_is_accepted_exactly_once()
    {
        var access = NewAccess();
        var ticket = access.IssueTicket();

        Assert.True(access.TryRedeemTicket(ticket));
        Assert.False(access.TryRedeemTicket(ticket));
    }

    [Fact]
    public async Task An_expired_ticket_is_rejected()
    {
        var access = NewAccess(ticket: TimeSpan.FromMilliseconds(30));
        var ticket = access.IssueTicket();

        await Task.Delay(250);

        Assert.False(access.TryRedeemTicket(ticket));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("to-nie-jest-bilet")]
    public void Missing_or_forged_tickets_are_rejected(string? ticket)
    {
        Assert.False(NewAccess().TryRedeemTicket(ticket));
    }

    [Fact]
    public void A_ticket_from_another_key_ring_is_rejected()
    {
        var ticket = NewAccess().IssueTicket();

        Assert.False(NewAccess().TryRedeemTicket(ticket));
    }

    [Fact]
    public void A_cookie_value_is_valid_but_a_ticket_is_not_a_cookie()
    {
        var provider = new EphemeralDataProtectionProvider();
        var access = NewAccess(provider);

        Assert.True(access.IsValidCookie(access.IssueCookieValue()));
        // Bilet i ciasteczko mają osobne cele ochrony — bilet nie może udawać ciasteczka.
        Assert.False(access.IsValidCookie(access.IssueTicket()));
    }

    [Fact]
    public async Task An_expired_cookie_is_rejected()
    {
        var access = NewAccess(cookie: TimeSpan.FromMilliseconds(30));
        var value = access.IssueCookieValue();

        await Task.Delay(250);

        Assert.False(access.IsValidCookie(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zmyslone")]
    public void Missing_or_forged_cookies_are_rejected(string? value)
    {
        Assert.False(NewAccess().IsValidCookie(value));
    }

    [Fact]
    public void The_dashboard_policy_allows_own_scripts_but_never_inline_or_eval()
    {
        // Panel potrzebuje własnych skryptów i stylów, ale to jedyna strona HTML w API — skrypty inline i eval zostają zakazane.
        var csp = JobsDashboardAccess.ContentSecurityPolicy;
        var scriptSrc = csp.Split(';').Select(d => d.Trim()).Single(d => d.StartsWith("script-src"));

        Assert.Equal("script-src 'self'", scriptSrc);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
    }
}
