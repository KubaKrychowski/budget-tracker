using System.Collections.Immutable;

namespace BudgetTracker.Identity.Models;

/// <summary>Dane ekranu zgody: kto prosi o dostęp, o jakie zakresy i w imieniu którego konta.</summary>
public sealed class AuthorizeViewModel
{
    public required string ApplicationName { get; init; }
    public required ImmutableArray<string> Scopes { get; init; }
    public required string UserEmail { get; init; }
}
