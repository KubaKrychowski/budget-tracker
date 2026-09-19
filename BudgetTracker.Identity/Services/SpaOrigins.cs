using BudgetTracker.Identity.Options;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Identity.Services;

/// <summary>
/// Originy klienta SPA wyprowadzone z jego zarejestrowanych <c>redirect_uri</c>. Służą do CORS
/// i do sprawdzania, czy adres powrotu wskazuje na naszą aplikację, a nie na obcą stronę (open redirect).
/// </summary>
public sealed class SpaOrigins(IOptions<SpaClientOptions> options)
{
    public IReadOnlyList<string> All { get; } = options.Value.RedirectUris
        .Select(uri => new Uri(uri).GetLeftPart(UriPartial.Authority))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Pierwszy zarejestrowany origin — dokąd wracamy, gdy żądanie nie wskazało własnego.</summary>
    public string? Default => All.Count > 0 ? All[0] : null;

    public bool IsAllowed(string origin) => All.Contains(origin, StringComparer.OrdinalIgnoreCase);
}
