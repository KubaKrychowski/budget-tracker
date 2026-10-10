using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.ErrorReporting;

/// <summary>Jedna linia logu zapamiętana w buforze.</summary>
public sealed record LogLine(DateTimeOffset At, LogLevel Level, string Category, string Message, string? TraceId);

/// <summary>
/// Pierścieniowy bufor ostatnich linii logu — źródło „logów" dołączanych do issue.
/// </summary>
/// <remarks>
/// Trzymamy tylko w pamięci, ostatnie <see cref="Capacity"/> linii. Zapamiętujemy <c>TraceId</c> bieżącej
/// aktywności, żeby do issue trafiały linie TEGO żądania, a nie cudze.
/// </remarks>
public sealed class RecentLogBuffer
{
    public const int Capacity = 500;

    private readonly ConcurrentQueue<LogLine> _lines = new();

    public void Add(LogLine line)
    {
        _lines.Enqueue(line);
        while (_lines.Count > Capacity && _lines.TryDequeue(out _)) { }
    }

    /// <summary>Linie danego żądania; gdy ich nie ma — ostatnie ostrzeżenia i błędy z dowolnego żądania.</summary>
    public IReadOnlyList<LogLine> For(string? traceId, int max)
    {
        var snapshot = _lines.ToArray();

        var own = traceId is null
            ? []
            : snapshot.Where(l => l.TraceId == traceId).TakeLast(max).ToArray();
        if (own.Length > 0) return own;

        return snapshot.Where(l => l.Level >= LogLevel.Warning).TakeLast(Math.Min(max, 20)).ToArray();
    }
}

/// <summary>Dostawca logów zasilający <see cref="RecentLogBuffer"/>.</summary>
public sealed class RecentLogProvider(RecentLogBuffer buffer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BufferLogger(categoryName, buffer);

    public void Dispose() { }

    private sealed class BufferLogger(string category, RecentLogBuffer buffer) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (exception is not null) message += $" [{exception.GetType().Name}: {exception.Message}]";

            buffer.Add(new LogLine(
                DateTimeOffset.UtcNow, logLevel, category, message, Activity.Current?.TraceId.ToString()));
        }
    }
}
