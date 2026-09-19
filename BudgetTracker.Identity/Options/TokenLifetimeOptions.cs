namespace BudgetTracker.Identity.Options;

/// <summary>
/// Czasy życia tokenów wystawianych przez serwer tożsamości. Krótki access token to jedyna realna
/// ochrona po jego kradzieży: API sprawdza tylko podpis i nie pyta Identity o unieważnienie, więc skradziony
/// token działa do <c>exp</c>, nawet po zmianie hasła albo wylogowaniu.
/// </summary>
/// <remarks>
/// SPA nie używa refresh tokena (odnawia sesję ukrytym iframe'em na ciasteczku Identity), więc
/// <see cref="RefreshToken"/> dotyczy wyłącznie <c>bt-cli</c>. Wartości to <see cref="TimeSpan"/> w formacie
/// konfiguracji <c>"00:15:00"</c>.
/// </remarks>
public sealed class TokenLifetimeOptions
{
    public const string SectionName = "Tokens";

    public TimeSpan AccessToken { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan IdentityToken { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshToken { get; init; } = TimeSpan.FromDays(14);
}
