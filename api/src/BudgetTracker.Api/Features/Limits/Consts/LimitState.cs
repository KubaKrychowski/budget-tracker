using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Limits.Consts;

/// <summary>Stan limitu w miesiącu — kolor paska na ekranie.</summary>
/// <remarks>
/// ⚠️ Konwerter na TYPIE, jak przy <c>MonthVerdict</c>: aplikacja nie rejestruje go globalnie, a front
/// porównuje stan z NAZWĄ — bez atrybutu każdy pasek wpadłby w gałąź domyślną.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<LimitState>))]
public enum LimitState
{
    /// <summary>Poniżej progu ostrzeżenia.</summary>
    Ok = 1,

    /// <summary>Od progu ostrzeżenia do 100% włącznie — limit jeszcze trzyma, ale jest blisko.</summary>
    Warning = 2,

    /// <summary>Wydane więcej niż limit.</summary>
    Over = 3,
}
