using Azure;
using Azure.Storage.Blobs;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.ML;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Aktywny model kategoryzacji zalogowanego użytkownika: wiersz z bazy + bajty z bloba.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ Model pobiera się RAZ NA WERSJĘ, nie raz na wiersz. Kategoryzator woła to dla każdej
/// transakcji importu, więc pobieranie bez pamięci podręcznej znaczyłoby kilkaset pobrań po ~250 KB
/// na jeden wyciąg.</item>
/// <item>Brak aktywnej wersji i brak bloba dają <c>null</c>, nie wyjątek: aplikacja ma działać na samych
/// regułach, dopóki ktoś nie uruchomi treningu (CLAUDE.md §3). Nowy użytkownik jest dokładnie w tym stanie.</item>
/// <item>Klucz pamięci niesie użytkownika ORAZ <c>ETag</c>: sam <c>ETag</c> jest tożsamością bloba,
/// a nie gwarantuje unikalności między kontenerami.</item>
/// </list>
/// </remarks>
public sealed class CategoryModelSource(
    AppDbContext db,
    BlobServiceClient blobServiceClient,
    ICurrentUserAccessor currentUser,
    IMemoryCache cache)
{
    /// <summary>Katalog wersji w kontenerze użytkownika — ta sama ścieżka, pod którą zapisuje <see cref="ModelStore"/>.</summary>
    private const string VersionsPrefix = "modelVersions/";

    /// <summary>
    /// Jeden kontekst ML.NET na proces: silniki muszą powstawać z tego samego kontekstu, w którym
    /// wczytano transformer, a tworzenie kontekstu per żądanie nic nie daje poza alokacją.
    /// </summary>
    private static readonly MLContext Ml = new();

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    public async Task<ActiveCategoryModel?> ActiveAsync(CancellationToken ct)
    {
        // Filtr własnościowy zawęża wersje do zalogowanego użytkownika — stąd brak jawnego WHERE po UserId.
        var active = await db.Set<ModelVersion>().FirstOrDefaultAsync(v => v.Active, ct);
        if (active is null) return null;

        var key = $"category-model:{currentUser.UserId}:{active.ETag}";
        if (cache.TryGetValue(key, out ActiveCategoryModel? cached) && cached is not null) return cached;

        var blob = UserBlobContainer.For(blobServiceClient, currentUser.UserId)
            .GetBlobClient(VersionsPrefix + active.Name);

        BinaryData content;
        try
        {
            content = (await blob.DownloadContentAsync(ct)).Value.Content;
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
        {
            // Wiersz bez bajtów: kontener albo blob zniknął. To stan „brak modelu”, nie awaria żądania.
            return null;
        }

        await using var stream = content.ToStream();
        var loaded = new ActiveCategoryModel(active.ETag, Ml, Ml.Model.Load(stream, out _));

        cache.Set(key, loaded, new MemoryCacheEntryOptions { SlidingExpiration = CacheLifetime });
        return loaded;
    }
}
