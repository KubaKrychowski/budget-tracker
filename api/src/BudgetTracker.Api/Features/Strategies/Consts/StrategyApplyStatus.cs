using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Strategies.Consts;

/// <summary>Co by się stało z akcją „do budżetu”, gdyby użytkownik zastosował strategię teraz.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StrategyApplyStatus>))]
public enum StrategyApplyStatus
{
    /// <summary>Akcja założy w budżecie nowy obiekt (cel, rezerwację, limit, zlecenie epizodyczne).</summary>
    New = 1,

    /// <summary>Akcja zmieni to, co w budżecie już jest (inna kwota celu albo limitu, zakończenie zlecenia stałego).</summary>
    Change = 2,

    /// <summary>To samo już jest w budżecie — akcja niczego by nie zmieniła i jest pominięta.</summary>
    Exists = 3,

    /// <summary>Miesiąc wykonania akcji w symulacji jeszcze nie nadszedł (albo akcja nigdy się nie wykonuje) — czeka.</summary>
    Waiting = 4,

    /// <summary>Akcja ma problem na tablicy (brakuje parametru) albo wskazany obiekt już nie istnieje — nie da się jej zastosować.</summary>
    Incomplete = 5,
}
