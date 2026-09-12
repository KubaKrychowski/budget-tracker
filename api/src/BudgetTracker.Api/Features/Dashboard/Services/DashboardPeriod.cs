namespace BudgetTracker.Api.Features.Dashboard.Services;

/// <summary>
/// Domyślny okres dashboardu, gdy zapytanie nie poda zakresu.
/// Wydzielone z endpointu, żeby dało się to przetestować bez stawiania hosta HTTP.
/// </summary>
public static class DashboardPeriod
{
    /// <summary>
    /// Musi odpowiadać DEFAULT_PERIOD_DAYS w <c>web/src/app/features/dashboard/dashboard.ts</c>.
    /// Rozjazd oznaczałby, że UI pokazuje inny okres niż ten, który API liczy bez parametrów.
    /// </summary>
    public const int DefaultDays = 30;

    /// <summary>
    /// Kroczące 30 dni kończące się dziś — NIE miesiąc kalendarzowy.
    /// Poprzednia reguła („od 1. dnia miesiąca do dziś") pierwszego dnia miesiąca dawała
    /// zakres jednodniowy, więc dashboard pokazywał same zera mimo pełnej historii.
    /// Zakres jest domknięty obustronnie, stąd DefaultDays - 1.
    /// </summary>
    public static (DateOnly From, DateOnly To) Default(DateOnly today) =>
        (today.AddDays(-(DefaultDays - 1)), today);
}
