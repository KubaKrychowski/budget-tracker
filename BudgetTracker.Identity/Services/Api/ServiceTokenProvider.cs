using System.Text.Json.Serialization;
using BudgetTracker.Identity.Infrastructure;

namespace BudgetTracker.Identity.Services.Api;

/// <summary>
/// Token serwisowy (client credentials) klienta <c>budgettracker-admin</c> do endpointów <c>/api/admin</c> API budżetu.
/// Serwer tożsamości prosi o niego SWÓJ WŁASNY endpoint <c>/connect/token</c> — tak samo jak każdy inny klient — i trzyma
/// go w pamięci do wygaśnięcia, zamiast prosić przy każdym poleceniu.
/// </summary>
/// <remarks>
/// Singleton: cache tokenu musi być wspólny dla żądań. Sekret klienta pochodzi z konfiguracji
/// (<see cref="OAuthDefaults.AdminClientSecretConfigKey"/>: user-secrets / zmienne środowiskowe), nie z repo.
/// </remarks>
public sealed class ServiceTokenProvider(
    IHttpClientFactory httpClientFactory, IConfiguration configuration, TimeProvider clock)
{
    public const string HttpClientName = "identity-self";

    /// <summary>Margines, o który token uznajemy za wygasły wcześniej — żeby nie wysłać go w ostatniej sekundzie życia.</summary>
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private DateTimeOffset expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (IsFresh()) return token!;

        await gate.WaitAsync(ct);
        try
        {
            if (IsFresh()) return token!;

            var secret = configuration[OAuthDefaults.AdminClientSecretConfigKey]
                ?? throw new UserDataServiceException($"Brak konfiguracji {OAuthDefaults.AdminClientSecretConfigKey}.");

            using var response = await SendAsync(secret, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new UserDataServiceException(
                    $"Serwer tożsamości odmówił tokenu serwisowego (HTTP {(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                ?? throw new UserDataServiceException("Pusta odpowiedź z endpointu tokenu.");

            token = body.AccessToken;
            expiresAt = clock.GetUtcNow().AddSeconds(body.ExpiresIn) - ExpiryMargin;
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string secret, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            return await client.PostAsync("connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = OAuthDefaults.AdminClientId,
                ["client_secret"] = secret,
                ["scope"] = OAuthDefaults.AdminApiScope,
            }), ct);
        }
        catch (HttpRequestException ex)
        {
            throw new UserDataServiceException("Nie udało się połączyć z własnym endpointem tokenu.", ex);
        }
    }

    private bool IsFresh() => token is not null && clock.GetUtcNow() < expiresAt;

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
