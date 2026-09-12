using BudgetTracker.Api.Features.Categorization.Contracts;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>Zbiór treningowy razem z informacją, skąd się wziął.</summary>
/// <param name="CorrectionDates">
/// Znaczniki `CreatedAt` WSZYSTKICH kwalifikujących się poprawek, w kolejności rosnącej.
///
/// Wygląda na szczegół, a jest po to, żeby handler nie musiał pytać bazy DRUGI RAZ o ten sam
/// zbiór wierszy tylko po to, by policzyć „ile przybyło od ostatniego treningu". Wcześniej
/// data była użyta w `OrderBy`, ale nie wynoszona do obiektu — więc jedno wejście na ekran
/// robiło dwa pełne przejścia po tym samym predykacie.
/// </param>
public sealed record TrainingSet(
    IReadOnlyList<TransactionFeatures> Rows,
    TrainingSetCompositionResponseDto Composition,
    IReadOnlyList<DateTimeOffset> CorrectionDates)
{
    /// <summary>Ile poprawek powstało PO wskazanej chwili — liczone w pamięci, bez zapytania.</summary>
    public int CorrectionsNewerThan(DateTimeOffset since) =>
        CorrectionDates.Count(d => d > since);
}
