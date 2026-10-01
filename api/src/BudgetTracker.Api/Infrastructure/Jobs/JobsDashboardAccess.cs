using System.Collections.Concurrent;
using System.Security.Cryptography;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.DataProtection;

namespace BudgetTracker.Api.Infrastructure.Jobs;

/// <summary>
/// Dostęp administratora do panelu Hangfire na produkcji: bilet jednorazowy z Identity zamieniany na krótkotrwałe ciasteczko.
/// </summary>
/// <remarks>
/// <para>
/// Przepływ: administrator klika „Zadania w tle” w panelu Identity → Identity (token serwisowy, polityka
/// <c>AdminPolicies.Admin</c>) prosi API o bilet → przeglądarka trafia na <c>/hangfire/enter?ticket=…</c> → API sprawdza
/// bilet, ustawia ciasteczko tylko na ścieżkę <c>/hangfire</c> i przekierowuje do panelu. Panel to zwykłe strony HTML
/// bez tokenu Bearer, więc innej drogi uwierzytelnienia niż ciasteczko nie ma.
/// </para>
/// <para>
/// ⚠️ Bilet jest ważny krótko (<see cref="TicketLifetime"/>) i jednorazowy — ląduje w adresie, więc trafia do logów
/// serwera i telemetrii. Po wykorzystaniu albo wygaśnięciu jest bezwartościowy. Bilety i ciasteczka są chronione kluczami
/// Data Protection procesu: po restarcie aplikacji tracą ważność i trzeba wejść ponownie przez Identity.
/// </para>
/// <para>
/// ⚠️ Zużyte bilety pamięta ten proces (w pamięci). Przy dwóch instancjach bilet dałoby się wykorzystać raz na każdej,
/// ale tylko w ciągu <see cref="TicketLifetime"/> i tylko z tymi samymi kluczami Data Protection.
/// </para>
/// </remarks>
public sealed class JobsDashboardAccess
{
    public const string CookieName = "hf_admin";
    public const string EnterPath = "/hangfire/enter";
    public const string DashboardPath = "/hangfire";

    private const string CookiePayload = "jobs-dashboard";

    /// <summary>
    /// Polityka treści dla stron panelu Hangfire. Skrypty tylko z własnego hosta (panel nie używa skryptów inline —
    /// konfigurację przekazuje atrybutami <c>data-</c>), style także inline, bo panel ma <c>style="…"</c> na paskach
    /// postępu. Reszta jak w reszcie API: brak osadzania w ramkach, brak <c>base</c>, formularze tylko do siebie.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self' data:; connect-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";

    private readonly ITimeLimitedDataProtector _tickets;
    private readonly ITimeLimitedDataProtector _cookies;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _usedTickets = new();

    public JobsDashboardAccess(IDataProtectionProvider provider)
    {
        _tickets = provider.CreateProtector("JobsDashboard.Ticket").ToTimeLimitedDataProtector();
        _cookies = provider.CreateProtector("JobsDashboard.Cookie").ToTimeLimitedDataProtector();
    }

    public TimeSpan TicketLifetime { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan CookieLifetime { get; init; } = TimeSpan.FromMinutes(30);

    public string IssueTicket() => _tickets.Protect(Guid.NewGuid().ToString("N"), TicketLifetime);

    /// <summary>Przyjmuje bilet dokładnie raz; wygasły, sfałszowany albo już użyty daje <c>false</c>.</summary>
    public bool TryRedeemTicket(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return false;

        string id;
        try
        {
            id = _tickets.Unprotect(ticket);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, until) in _usedTickets)
        {
            if (until < now) _usedTickets.TryRemove(key, out _);
        }

        return _usedTickets.TryAdd(id, now + TicketLifetime);
    }

    public string IssueCookieValue() => _cookies.Protect(CookiePayload, CookieLifetime);

    public bool IsValidCookie(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            return _cookies.Unprotect(value) == CookiePayload;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

/// <summary>
/// Autoryzacja panelu Hangfire: ważne ciasteczko administratora, a w Development dodatkowo połączenie lokalne.
/// </summary>
public sealed class JobsDashboardAuthorizationFilter(bool allowLocalRequests) : IDashboardAuthorizationFilter
{
    private static readonly LocalRequestsOnlyAuthorizationFilter Local = new();

    public bool Authorize(DashboardContext context)
    {
        if (allowLocalRequests && Local.Authorize(context)) return true;

        var http = context.GetHttpContext();
        var access = (JobsDashboardAccess?)http.RequestServices.GetService(typeof(JobsDashboardAccess));

        return access?.IsValidCookie(http.Request.Cookies[JobsDashboardAccess.CookieName]) ?? false;
    }
}
