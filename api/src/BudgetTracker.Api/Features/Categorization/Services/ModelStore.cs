using System.Globalization;
using Azure.Storage.Blobs;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetTracker.Api.Features.Categorization.Services;

public sealed class ModelStore(
    TimeProvider clock,
    AppDbContext db,
    BlobServiceClient blobServiceClient,
    IConfiguration configuration,
    ILogger<ModelStore>? logger = null)
{
    private readonly ILogger<ModelStore> _logger = logger ?? NullLogger<ModelStore>.Instance;

    /// <param name="userId">
    /// Właściciel modelu — przychodzi ARGUMENTEM, a nie z <c>ICurrentUserAccessor</c>.
    /// ⚠️ Publikacja idzie z zadania w tle, gdzie nie ma ani żądania HTTP, ani tokenu; czytanie
    /// użytkownika „skądś” kończyło się tam pustką, a w efekcie modelem zapisanym pod nieswoim kontem
    /// albo wcale. Identyfikator niesie zgłoszenie treningu i to jedyne miejsce, z którego wolno go brać.
    /// </param>
    /// <param name="jobId">Zgłoszenie, które ten trening uruchomiło; <c>null</c>, gdy trening szedł z CLI.</param>
    /// <remarks>
    /// ⚠️ <b>Brak kontenera użytkownika nie przerywa publikacji</b> — kontener jest zakładany w locie. Wcześniej to był twardy
    /// błąd (<c>ContainerNotFoundException</c>), bo kontener miał powstawać RAZ przy koncie, a jego brak miał być widoczny.
    /// W praktyce zamieniało to trening, który już się policzył, w stracony przebieg (zadanie bez ponowień) za błąd, którego
    /// użytkownik nie umie naprawić — a konto bez kontenera powstaje, gdy wywołanie serwera tożsamości przy rejestracji
    /// nie doszło do skutku. Zakładanie jest idempotentne i dotyczy wyłącznie kontenera właściciela zadania, więc nic tu
    /// nie zyskuje dostępu do cudzych danych. Widoczność zostaje w logu (ostrzeżenie), nie w przerwanym treningu.
    /// </remarks>
    public async Task<Guid> Publish(
        (TrainingReportResponseDto, MemoryStream? buffer) report,
        Guid userId,
        string? jobId,
        CancellationToken ct)
    {
        // ⚠️ Pusty identyfikator dałby kontener „00000000-…" wspólny dla każdego, kto go kiedyś dostanie — a od kiedy
        // brakujący kontener zakładamy w locie, nie wolno nawet zacząć.
        if (userId == Guid.Empty) throw new ArgumentException("Publikacja modelu wymaga identyfikatora właściciela.", nameof(userId));

        var now = clock.GetUtcNow();
        var version = now.UtcDateTime.ToString(configuration["Formats:Version"], CultureInfo.InvariantCulture);
        var modelName = $"model-{version}.zip";

        var blobContainerClient = UserBlobContainer.For(blobServiceClient, userId);

        var created = await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: ct);
        if (created is not null)
        {
            _logger.LogWarning(
                "Konto {UserId} nie miało kontenera na modele — założono go przy publikacji. " +
                "Jeśli kontener powinien już istnieć, sprawdź Storage:BlobServiceUri i to, czy rejestracja wywołała /container.",
                userId);
        }

        var eTag = (await blobContainerClient.UploadBlobAsync($"modelVersions/{modelName}", report.buffer, ct))
            .Value.ETag.ToString();

        var modelVersion = new ModelVersion(
            userId, 
            false, 
            modelName, 
            report.Item1.Rows, 
            report.Item1.Categories,
            Convert.ToDecimal(report.Item1.MicroAccuracy),
            Convert.ToDecimal(report.Item1.MacroAccuracy),
            eTag,
            jobId);

        await db.AddAsync(modelVersion, ct);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Opublikowano model {ModelName} dla konta {UserId} (zgłoszenie {JobId}): {Rows} przykładów, {Categories} klas, ETag {ETag}, wersja {VersionId}.",
            modelName, userId, jobId, report.Item1.Rows, report.Item1.Categories, eTag, modelVersion.BusinessId);

        return modelVersion.BusinessId;
    }
}