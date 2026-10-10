using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BudgetTracker.ErrorReporting;

/// <summary>Odcisk błędu do deduplikacji: ten sam błąd w tym samym miejscu = ten sam odcisk.</summary>
/// <remarks>
/// Liczony z typu wyjątku i pierwszych ramek stosu BEZ numerów linii — przesunięcie kodu przy
/// kolejnym wdrożeniu nie powinno zakładać drugiego issue dla tej samej awarii.
/// Komunikat wyjątku celowo nie wchodzi do odcisku: zwykle niesie wartości (identyfikatory, kwoty).
/// </remarks>
public static partial class ErrorFingerprint
{
    private const int FramesUsed = 5;

    [GeneratedRegex(@"\s+in\s+.+?:line\s+\d+")]
    private static partial Regex SourceLocation();

    public static string Compute(Exception exception)
    {
        var root = exception;
        while (root.InnerException is not null) root = root.InnerException;

        var frames = (root.StackTrace ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(FramesUsed)
            .Select(f => SourceLocation().Replace(f, string.Empty));

        var material = $"{root.GetType().FullName}|{string.Join("|", frames)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..12].ToLowerInvariant();
    }
}
