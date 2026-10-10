namespace BudgetTracker.ErrorReporting;

/// <summary>Zdarzenie 500 gotowe do zgłoszenia. Zawiera wyłącznie metodę i ścieżkę — bez query stringa, nagłówków i ciała.</summary>
public sealed record ErrorReport(
    DateTimeOffset At,
    string Service,
    string Method,
    string Path,
    string? TraceId,
    Exception Exception,
    IReadOnlyList<LogLine> Logs);
