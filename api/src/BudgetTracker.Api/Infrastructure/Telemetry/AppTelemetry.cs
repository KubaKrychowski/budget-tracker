using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BudgetTracker.Api.Infrastructure.Telemetry;

/// <summary>Własne źródła telemetrii aplikacji: <see cref="Meter"/> z metrykami i <see cref="ActivitySource"/> ze spanami.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Obie nazwy muszą być zarejestrowane w <c>TelemetryModule</c> (<c>AddMeter</c>/<c>AddSource</c>) — niezarejestrowane
/// źródło działa, ale nic z niego nie wychodzi, i nie ma żadnego błędu.</item>
/// <item>⚠️ Etykiety (tagi) tylko o niskiej kardynalności: nazwa kroku, wynik. Nigdy identyfikator użytkownika, budżetu
/// ani opis transakcji — to kosztuje przy liczbie szeregów w Application Insights i jest daną osobową.</item>
/// </list>
/// </remarks>
public static class AppTelemetry
{
    /// <summary>Nazwa źródeł. Jedna dla metryk i spanów, żeby w portalu było je widać razem.</summary>
    public const string Name = "BudgetTracker.Api";

    /// <summary>Metryki aplikacji.</summary>
    public static readonly Meter Meter = new(Name);

    /// <summary>Spany aplikacji — podkroki, których nie widać w automatycznej instrumentacji żądań.</summary>
    public static readonly ActivitySource Source = new(Name);

    /// <summary>Czas podglądu importu (krok 3 steppera), w milisekundach.</summary>
    public static readonly Histogram<double> ImportPreviewDuration =
        Meter.CreateHistogram<double>("budgettracker.import.preview.duration", "ms", "Czas podglądu importu wyciągu.");

    /// <summary>Liczba wierszy w podglądzie importu — pozwala odróżnić wolny import od dużego pliku.</summary>
    public static readonly Histogram<int> ImportPreviewRows =
        Meter.CreateHistogram<int>("budgettracker.import.preview.rows", "{row}", "Liczba wierszy w podglądzie importu.");

    /// <summary>Czas zapisu zatwierdzonego importu, w milisekundach.</summary>
    public static readonly Histogram<double> ImportCommitDuration =
        Meter.CreateHistogram<double>("budgettracker.import.commit.duration", "ms", "Czas zapisu zatwierdzonego importu.");

    /// <summary>Czas treningu modelu kategoryzacji, w sekundach. Tag <c>outcome</c>: <c>success</c> albo <c>failure</c>.</summary>
    public static readonly Histogram<double> TrainingDuration =
        Meter.CreateHistogram<double>("budgettracker.categorization.training.duration", "s", "Czas treningu modelu kategoryzacji.");

    /// <summary>Mierzy czas wykonania <paramref name="operation"/> i zapisuje go w <paramref name="histogram"/> (ms).</summary>
    public static async Task<T> MeasureAsync<T>(
        Histogram<double> histogram, Func<Task<T>> operation, params KeyValuePair<string, object?>[] tags)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return await operation();
        }
        finally
        {
            histogram.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds, tags);
        }
    }
}
