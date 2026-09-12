using BudgetTracker.Api.Features.Categorization.Contracts;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>Wpis historii: która wersja, kiedy powstała i z jakim wynikiem.</summary>
/// <param name="TrainedAt">
/// Moment ZAKOŃCZENIA treningu. Służy też za punkt odniesienia dla „przybyło N poprawek",
/// więc musi być czasem rzeczywistym, nie czasem nazwy pliku (te są równe, ale zależność
/// w drugą stronę byłaby przypadkowa).
/// </param>
public sealed record TrainingHistoryEntry(
    string Version,
    DateTimeOffset TrainedAt,
    TrainingReportResponseDto Report);
