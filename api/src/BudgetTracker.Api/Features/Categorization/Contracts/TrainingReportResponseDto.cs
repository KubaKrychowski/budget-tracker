namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Metryki treningu — do zaraportowania użytkownikowi, nie do logu, który nikt nie czyta.</summary>
/// <param name="Rows">Liczba przykładów treningowych.</param>
/// <param name="Categories">Liczba rozpoznawanych klas.</param>
/// <param name="MicroAccuracy">Trafność liczona per przykład — dominują ją klasy liczne.</param>
/// <param name="MacroAccuracy">
/// Średnia trafność per klasa. Przy bardzo nierównym rozkładzie (setki przykładów jednej kategorii,
/// pojedyncze innej) jest niższa i uczciwiej pokazuje, jak model radzi sobie z rzadkimi kategoriami.
/// </param>
public sealed record TrainingReportResponseDto(int Rows, int Categories, double MicroAccuracy, double MacroAccuracy);
