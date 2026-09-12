using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Savings.Consts;

/// <summary>
/// Status rezerwacji — dokładnie trzy z makiety „Ekran — Wszystkie rezerwacje" (171:1690).
/// </summary>
/// <remarks>
/// ⚠️ Konwerter na TYPIE, bo aplikacja nie rejestruje globalnego <c>JsonStringEnumConverter</c>.
/// Bez niego status jedzie do frontu jako <c>0..2</c>, front porównuje go z nazwą i każdy wiersz
/// wpada w gałąź domyślną. Ta sama pułapka co przy <see cref="MonthVerdict"/> — tam wyszła
/// dopiero na żywym API.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ReservationStatus>))]
public enum ReservationStatus
{
    /// <summary>Zbiera — termin jeszcze przed nami.</summary>
    Collecting = 1,

    /// <summary>
    /// Po terminie i nierozliczona. Osobny status, bo taka rezerwacja zaniża wolne środki
    /// w nieskończoność, a jedyne, co ją zamknie, to pamięć użytkownika.
    /// </summary>
    Overdue = 2,

    /// <summary>Rozliczona — wskazano realną wypłatę.</summary>
    Settled = 3,
}
