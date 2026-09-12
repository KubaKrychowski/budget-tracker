using System.Text.Json.Serialization;
using BudgetTracker.Api.Features.Transactions.Consts;

namespace BudgetTracker.Api.Features.Savings.Consts;

/// <summary>
/// Werdykt miesiąca. Trzy wartości i tylko pierwsza jest dowodem odporności —
/// reszta to stany informacyjne.
/// </summary>
/// <remarks>
/// ⚠️ Konwerter jest KONIECZNY. Aplikacja nie rejestruje globalnego
/// <c>JsonStringEnumConverter</c>, więc bez niego werdykt jedzie do frontu jako <c>0..3</c>,
/// a front porównuje go z nazwą — każdy miesiąc wpadłby wtedy w gałąź domyślną i tabela
/// pokazałaby „bez celu" dla wszystkiego. Wyłapane dopiero na żywym API: testy jednostkowe
/// karmią front gotowym JSON-em z nazwami, więc tej różnicy nie widzą.
/// Konwerter na TYPIE, nie globalnie — ta sama decyzja co przy <c>TransactionDirection</c>.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<MonthVerdict>))]
public enum MonthVerdict
{
    /// <summary>Wtedy nie było jeszcze celu — nie ma czego oceniać.</summary>
    NoGoal = 1,

    /// <summary>Odłożono mniej niż cel. ⚠️ To informacja, nie ocena — patrz teksty na ekranie.</summary>
    GoalMissed = 2,

    /// <summary>Cel osiągnięty, ale miesiąc był spokojny — miła wiadomość, nie dowód.</summary>
    GoalMet = 3,

    /// <summary>Cel osiągnięty MIMO jednorazowego wydatku. Jedyny werdykt, który coś dowodzi.</summary>
    Proof = 4,
}
