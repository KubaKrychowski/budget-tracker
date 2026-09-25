namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Wynik zgłoszonego treningu albo informacja, że jeszcze go nie ma.</summary>
/// <param name="Ready">
/// Czy trening się zakończył i zostawił wersję modelu. ⚠️ <c>false</c> obejmuje też nieudany przebieg —
/// z punktu widzenia ekranu „nie ma wyniku” to jeden stan, a szczegóły awarii są w panelu Hangfire.
/// </param>
/// <param name="Report">Metryki wersji, którą ten trening wyprodukował; <c>null</c>, dopóki jej nie ma.</param>
public sealed record TrainingStatusResponseDto(bool Ready, TrainingReportResponseDto? Report)
{
    /// <summary>Odpowiedź „jeszcze nic nie ma” — jedna instancja, bo nie niesie żadnych danych.</summary>
    public static TrainingStatusResponseDto Pending { get; } = new(false, null);
}
