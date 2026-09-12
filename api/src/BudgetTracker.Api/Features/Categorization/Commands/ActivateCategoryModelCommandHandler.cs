using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Services;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>Powrót do wcześniejszej wersji modelu. Nic nie kasuje — droga działa w obie strony.</summary>
/// <remarks>
/// Metoda synchroniczna, bo cała operacja to kopia i atomowa podmiana pliku. Wymaga tej samej blokady co trening,
/// więc w trakcie importu albo treningu kończy się 409.
/// </remarks>
public sealed class ActivateCategoryModelCommandHandler(ModelStore store)
{
    public void Handle(string version)
    {
        using var lease = store.TryBeginTraining() ?? throw new TrainingBusyException();
        store.Activate(lease, version);
    }
}
