namespace BudgetTracker.ErrorReporting;

/// <summary>Ustawienia zakładania issues na GitHubie przy błędzie 500 (sekcja <c>ErrorReporting</c>).</summary>
/// <remarks>
/// ⚠️ Bez <see cref="Token"/> funkcja jest wyłączona — lokalny Rider i testy nie wołają GitHuba.
/// Token trzymaj w sekretach (user-secrets, zmienna środowiskowa <c>ErrorReporting__Token</c>), nigdy w pliku.
/// Wystarczy uprawnienie „Issues: read and write" na repozytorium z <see cref="Repository"/>.
/// </remarks>
public sealed class ErrorReportingOptions
{
    public const string SectionName = "ErrorReporting";

    /// <summary>Repozytorium docelowe w formie <c>właściciel/nazwa</c>.</summary>
    public string Repository { get; set; } = "KubaKrychowski/budget-tracker-2-boards";

    /// <summary>Token GitHub (PAT albo token instalacji GitHub App). Pusty = funkcja wyłączona.</summary>
    public string? Token { get; set; }

    /// <summary>Nazwa serwisu w tytule i treści issue (odróżnia API od Identity).</summary>
    public string Service { get; set; } = "api";

    /// <summary>Etykiety nadawane zakładanym issues.</summary>
    public string[] Labels { get; set; } = ["bug", "auto-500"];

    /// <summary>Ile linii logu dołączyć do issue.</summary>
    public int MaxLogLines { get; set; } = 40;

    /// <summary>Przez ile minut ten sam błąd nie jest zgłaszany ponownie z tego procesu.</summary>
    public int CooldownMinutes { get; set; } = 60;

    public bool Enabled => !string.IsNullOrWhiteSpace(Token) && Repository.Contains('/');
}
