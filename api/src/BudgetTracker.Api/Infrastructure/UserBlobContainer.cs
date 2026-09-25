using Azure.Storage.Blobs;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>Magazyn plików użytkownika — JEDNO miejsce, w którym powstaje nazwa jego kontenera.</summary>
/// <remarks>
/// ⚠️ Nazwa sklejana w kilku miejscach to zaproszenie do wpadki, której nie widać: literówka albo inny
/// format identyfikatora nie dają błędu, tylko pusty (cudzy) kontener. Kod ma pytać o kontener przez tę
/// klasę i nigdy nie budować nazwy samodzielnie.
///
/// Format musi spełniać reguły Azure: 3–63 znaki, małe litery, cyfry i myślniki, początek alfanumeryczny.
/// <c>Guid.ToString()</c> daje 36 znaków i same małe szesnastkowe — łapie się z zapasem.
/// </remarks>
public static class UserBlobContainer
{
    public static string NameFor(Guid userId) => userId.ToString();

    public static BlobContainerClient For(BlobServiceClient service, Guid userId) =>
        service.GetBlobContainerClient(NameFor(userId));
}
