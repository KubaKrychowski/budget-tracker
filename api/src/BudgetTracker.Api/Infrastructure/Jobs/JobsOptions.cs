namespace BudgetTracker.Api.Infrastructure.Jobs;

/// <summary>Ustawienia zadań cyklicznych z <c>appsettings.json</c> (sekcja <c>Jobs</c>).</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>
    /// Ile dni Hangfire trzyma historię zakończonych przebiegów.
    /// </summary>
    /// <remarks>
    /// ⚠️ Historia przebiegów JEST audytem zadań. Domyślnie Hangfire kasuje udane przebiegi po dobie,
    /// więc bez tego ustawienia audyt byłby pusty następnego dnia.
    /// </remarks>
    public int HistoryRetentionDays { get; set; } = 90;
}
