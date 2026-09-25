using System.Net.Http.Headers;
using System.Text.Json;

namespace BudgetTracker.Identity.Services.Api;

/// <summary>Klient HTTP endpointów <c>/api/admin</c> API budżetu, z tokenem serwisowym z <see cref="ServiceTokenProvider"/>.</summary>
public sealed class ApiUserDataClient(IHttpClientFactory httpClientFactory, ServiceTokenProvider tokens) : IUserDataClient
{
    public const string HttpClientName = "user-data-api";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyDictionary<Guid, OwnerDataCounts>> GetSummariesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var response = await SendAsync<SummariesResponse>(
            HttpMethod.Post, "api/admin/users/data-summary", new { userIds }, ct);

        return response.Users.ToDictionary(u => u.UserId, u => u.Counts);
    }

    public async Task<IReadOnlyList<OrphanedOwner>> GetOrphansAsync(
        IReadOnlyCollection<Guid> knownUserIds, CancellationToken ct) =>
        (await SendAsync<OrphansResponse>(HttpMethod.Post, "api/admin/orphans", new { knownUserIds }, ct)).Owners;

    public async Task<OwnerDataCounts> DeleteDataAsync(Guid ownerId, CancellationToken ct) =>
        (await SendAsync<ChangeResponse>(HttpMethod.Delete, $"api/admin/owners/{ownerId}/data", null, ct)).Counts;

    public async Task<OwnerDataCounts> ReassignAsync(Guid ownerId, Guid targetUserId, CancellationToken ct) =>
        (await SendAsync<ChangeResponse>(
            HttpMethod.Post, $"api/admin/owners/{ownerId}/reassign", new { targetUserId }, ct)).Counts;

    /// <summary>
    /// Zakłada magazyn plików użytkownika po stronie API. Powtórzenie jest bezpieczne.
    /// </summary>
    /// <remarks>
    /// ⚠️ API odpowiada BEZ TREŚCI, więc nie da się tego przepuścić przez <see cref="SendAsync{T}"/> —
    /// tamta metoda wymaga ciała odpowiedzi i pusta zakończyłaby się wyjątkiem o „pustej odpowiedzi”.
    /// </remarks>
    public Task CreateUserContainerAsync(Guid userId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"api/admin/users/{userId}/container", null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, body, ct);

        return await ReadAsync<T>(response, method, url, ct);
    }

    /// <summary>Wariant dla poleceń bez treści odpowiedzi (2xx bez ciała).</summary>
    private async Task SendAsync(HttpMethod method, string url, object? body, CancellationToken ct) =>
        (await SendCoreAsync(method, url, body, ct)).Dispose();

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var accessToken = await tokens.GetTokenAsync(ct);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        try
        {
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new UserDataServiceException($"API odrzuciło polecenie {method} {url} (HTTP {status}).");
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            throw new UserDataServiceException($"Nie udało się połączyć z API ({method} {url}).", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new UserDataServiceException($"API nie odpowiedziało na czas ({method} {url}).", ex);
        }
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response, HttpMethod method, string url, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(Json, ct)
        ?? throw new UserDataServiceException($"Pusta odpowiedź API na {method} {url}.");

    private sealed record SummariesResponse(List<SummaryItem> Users);

    private sealed record SummaryItem(Guid UserId, OwnerDataCounts Counts);

    private sealed record OrphansResponse(List<OrphanedOwner> Owners);

    private sealed record ChangeResponse(OwnerDataCounts Counts);
}
