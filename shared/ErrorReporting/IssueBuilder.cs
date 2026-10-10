using System.Text;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.ErrorReporting;

/// <summary>Składa tytuł i treść issue. Wszystko, co pochodzi z wyjątku i logów, przechodzi przez <see cref="ErrorMasker"/>.</summary>
public static class IssueBuilder
{
    private const int MaxTitle = 120;
    private const int MaxStack = 12_000;

    /// <summary>Znacznik w treści, po którym wyszukujemy już założone issue tego błędu.</summary>
    public static string Marker(string fingerprint) => $"BT-FP-{fingerprint}";

    public static string Title(ErrorReport report, string fingerprint)
    {
        var message = ErrorMasker.Sanitize(report.Exception.Message).ReplaceLineEndings(" ");
        var title = $"500 [{report.Service}] {report.Exception.GetType().Name}: {message}";
        if (title.Length > MaxTitle) title = title[..(MaxTitle - 1)] + "…";
        return $"{title} ({fingerprint[..6]})";
    }

    public static string Body(ErrorReport report, string fingerprint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Issue założone automatycznie po nieobsłużonym wyjątku (HTTP 500).");
        sb.AppendLine();
        sb.AppendLine($"- **Serwis:** {report.Service}");
        sb.AppendLine($"- **Czas (UTC):** {report.At:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **Żądanie:** `{report.Method} {ErrorMasker.Sanitize(report.Path)}`");
        sb.AppendLine($"- **TraceId:** `{report.TraceId ?? "brak"}`");
        sb.AppendLine($"- **Odcisk:** `{Marker(fingerprint)}`");
        sb.AppendLine();
        sb.AppendLine("## Stack trace");
        sb.AppendLine();
        sb.AppendLine("```");
        var stack = ErrorMasker.Sanitize(report.Exception.ToString());
        if (stack.Length > MaxStack) stack = stack[..MaxStack] + "\n… (obcięte)";
        sb.AppendLine(Defang(stack));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Logi");
        sb.AppendLine();
        if (report.Logs.Count == 0)
        {
            sb.AppendLine("_Brak zapamiętanych linii logu._");
        }
        else
        {
            sb.AppendLine("```");
            foreach (var l in report.Logs)
            {
                var line = $"{l.At:HH:mm:ss.fff} {Short(l.Level)} {l.Category}: {l.Message}";
                sb.AppendLine(Defang(ErrorMasker.Sanitize(line).ReplaceLineEndings(" ")));
            }
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    /// <summary>Potrójny backtick w treści zamknąłby blok kodu i pozwolił „dopisać" markdown do issue.</summary>
    private static string Defang(string text) => text.Replace("```", "ʼʼʼ");

    private static string Short(LogLevel level) => level switch
    {
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => level.ToString()[..3].ToUpperInvariant(),
    };
}
