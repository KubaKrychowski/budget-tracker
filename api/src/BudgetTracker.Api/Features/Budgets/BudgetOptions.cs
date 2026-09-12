namespace BudgetTracker.Api.Features.Budgets;

/// <summary>
/// Ustawienia budżetów z <c>appsettings.json</c> (sekcja <c>Budgets</c>).
/// </summary>
public sealed class BudgetOptions
{
    public const string SectionName = "Budgets";

    /// <summary>
    /// Ile dni usunięty budżet daje się przywrócić, zanim zniknie fizycznie.
    ///
    /// ⚠️ To jest OBIETNICA SKŁADANA UŻYTKOWNIKOWI — modal usuwania mówi „odwracalna przez
    /// N dni" i bierze N stąd, a nie z własnego tekstu. Gdyby front miał swoją liczbę,
    /// zmiana tutaj po cichu zamieniłaby komunikat w kłamstwo.
    ///
    /// Ustawienie, nie stała w kodzie: w testach skracamy je do zera, żeby sprzątanie dało
    /// się sprawdzić bez przesuwania zegara o miesiąc.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Harmonogram sprzątania usuniętych budżetów (cron, UTC) — domyślnie raz na dobę o 3:00.
    /// Pełna doba wystarcza: próg liczy się w dniach, a opóźnienie o kilka godzin nikomu nie szkodzi.
    /// </summary>
    public string PurgeCron { get; set; } = "0 3 * * *";
}
