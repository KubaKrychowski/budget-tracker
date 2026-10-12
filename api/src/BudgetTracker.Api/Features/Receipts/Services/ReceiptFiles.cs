using Azure.Storage.Blobs;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Pliki paragonów w kontenerze blob użytkownika oraz reguły, jakie pliki przyjmujemy.</summary>
/// <remarks>
/// ⚠️ Nazwę blobu nadaje serwer (<c>receipts/{guid}{rozszerzenie}</c>), nigdy nazwa z uploadu — ścieżka z pliku
/// pozwalałaby nadpisać cudzy blob albo wyjść poza prefiks. Rozszerzenie wynika z typu zawartości, a nie z nazwy pliku.
/// </remarks>
public sealed class ReceiptFiles(BlobServiceClient blobServiceClient)
{
    /// <summary>Górny limit rozmiaru — zdjęcie z telefonu to kilka MB, a model Azure i tak przyjmuje do 500 MB.</summary>
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    /// <summary>Typy obsługiwane przez model paragonów Azure → rozszerzenie blobu.</summary>
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/heic"] = ".heic",
        ["image/heif"] = ".heif",
        ["application/pdf"] = ".pdf",
    };

    public static bool IsSupported(string contentType) => Extensions.ContainsKey(contentType);

    public static string NewBlobName(string contentType) => $"receipts/{Guid.CreateVersion7():N}{Extensions[contentType]}";

    private BlobContainerClient Container(Guid userId) => UserBlobContainer.For(blobServiceClient, userId);

    public async Task SaveAsync(Guid userId, string blobName, BinaryData content, string contentType, CancellationToken ct)
    {
        var container = Container(userId);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        await container.GetBlobClient(blobName).UploadAsync(
            content, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType },
            }, ct);
    }

    public async Task<Stream> OpenAsync(Guid userId, string blobName, CancellationToken ct) =>
        await Container(userId).GetBlobClient(blobName).OpenReadAsync(cancellationToken: ct);

    public async Task DeleteAsync(Guid userId, string blobName, CancellationToken ct) =>
        await Container(userId).GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
}
