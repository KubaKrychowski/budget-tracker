using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Services;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>
/// Douczanie modelu — na zbiorze z DWÓCH źródeł (plik bazowy + poprawki) i z zapisem,
/// który nie niszczy poprzedniego modelu.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Blokada PRZED zbudowaniem zbioru: składanie go czyta bazę i plik, więc nie ma sensu zaczynać,
/// jeśli i tak nie wolno opublikować wyniku. W trakcie importu kończy się 409.</item>
/// <item>Zapis idzie do pliku tymczasowego, nie pod ścieżkę aktywnego modelu — patrz <see cref="ModelStore"/>.
/// Trening mógł się wywalić po utworzeniu pliku; <c>Publish</c> go przenosi, więc na końcu zostaje do usunięcia
/// tylko śmieć po nieudanej próbie.</item>
/// </list>
/// </remarks>
public sealed class TrainCategoryModelCommandHandler(TrainingSetBuilder builder, ModelStore store)
{
    public async Task<TrainingReportResponseDto> HandleAsync(CancellationToken ct)
    {
        using var lease = store.TryBeginTraining() ?? throw new TrainingBusyException();

        var set = await builder.BuildAsync(ct);
        if (set.Rows.Count == 0) throw new TrainingDataMissingException();

        var staging = store.CreateStagingPath();
        try
        {
            var report = CategoryModelTrainer.Train(set.Rows, staging);
            store.Publish(lease, staging, report);
            return report;
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }
}
