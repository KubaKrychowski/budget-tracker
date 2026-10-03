using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Strategies.Consts;

/// <summary>Od czego startuje nowa strategia.</summary>
/// <remarks>
/// ⚠️ Konwerter na TYPIE, bo aplikacja nie rejestruje globalnego <c>JsonStringEnumConverter</c>. Enum jedzie tylko przez
/// kontrakt HTTP, nie do bazy.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StrategyTemplate>))]
public enum StrategyTemplate
{
    /// <summary>Pusta tablica.</summary>
    Blank = 1,

    /// <summary>
    /// Szablon „kredyt i poduszka”: nadwyżka, kredyt, premia z nadpłatą, warunek spłaty reszty i poduszka docelowa.
    /// Liczby w nim są PRZYKŁADOWE — użytkownik wpisuje własne.
    /// </summary>
    LoanAndCushion = 2,
}
