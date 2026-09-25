using System.Globalization;
using Azure.Storage.Blobs;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Categorization.Services;

public sealed class ModelStore(
    TimeProvider clock,
    AppDbContext db,
    BlobServiceClient blobServiceClient,
    IConfiguration configuration)
{
    /// <param name="userId">
    /// Właściciel modelu — przychodzi ARGUMENTEM, a nie z <c>ICurrentUserAccessor</c>.
    /// ⚠️ Publikacja idzie z zadania w tle, gdzie nie ma ani żądania HTTP, ani tokenu; czytanie
    /// użytkownika „skądś” kończyło się tam pustką, a w efekcie modelem zapisanym pod nieswoim kontem
    /// albo wcale. Identyfikator niesie zgłoszenie treningu i to jedyne miejsce, z którego wolno go brać.
    /// </param>
    /// <param name="jobId">Zgłoszenie, które ten trening uruchomiło; <c>null</c>, gdy trening szedł z CLI.</param>
    public async Task<Guid> Publish(
        (TrainingReportResponseDto, MemoryStream? buffer) report,
        Guid userId,
        string? jobId,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var version = now.UtcDateTime.ToString(configuration["Formats:Version"], CultureInfo.InvariantCulture);
        var modelName = $"model-{version}.zip";

        var blobContainerClient = UserBlobContainer.For(blobServiceClient, userId);

        if (!await blobContainerClient.ExistsAsync(ct))
        {
            throw new ContainerNotFoundException(userId);
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

        return modelVersion.BusinessId;
    }
}