using Azure.Storage.Blobs;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Admin.Commands;

/// <summary>Zakłada magazyn plików konta — kontener na modele i artefakty użytkownika.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ Woła to serwer TOŻSAMOŚCI zaraz po założeniu konta, bo to on wie, że konto powstało.
/// API nie ma listy użytkowników i nie ma jak tego zauważyć samo.</item>
/// <item>Powtórzenie jest bezpieczne (<c>CreateIfNotExists</c>) — ponowione żądanie po nieudanej próbie
/// ma dokończyć robotę, a nie wywrócić się na tym, że kontener już jest.</item>
/// <item>Kontener zakłada się przy koncie, ale to NIE jedyna droga: publikacja modelu (<c>ModelStore</c>) zakłada go
/// w locie, gdy go brakuje, i loguje ostrzeżenie — konto bez kontenera (rejestracja nie dotarła do API) nie może
/// kosztować użytkownika już policzonego treningu.</item>
/// </list>
/// </remarks>
public sealed class CreateUserStorageCommandHandler(BlobServiceClient blobServiceClient)
{
    public async Task HandleAsync(Guid userId, CancellationToken ct) =>
        await UserBlobContainer.For(blobServiceClient, userId).CreateIfNotExistsAsync(cancellationToken: ct);
}
