using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.StandingOrders.Consts;

/// <summary>Stan zlecenia stałego w oglądanym miesiącu — kolumna „W tym miesiącu”.</summary>
/// <remarks>Konwerter na TYPIE, jak przy <c>LimitState</c>: front porównuje stan z nazwą.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StandingOrderMonthState>))]
public enum StandingOrderMonthState
{
    /// <summary>Zeszło w zwykłej kwocie.</summary>
    Paid = 1,

    /// <summary>Zeszło, ale w innej kwocie niż zwykle (np. podwyżka) — informacja, nie błąd.</summary>
    PaidDifferentAmount = 2,

    /// <summary>Miesiąc trwa, a zlecenie jeszcze nie zeszło.</summary>
    Waiting = 3,

    /// <summary>Miesiąc zamknięty, a zlecenie nie zeszło.</summary>
    Missed = 4,

    /// <summary>Zlecenie nie przypada na ten miesiąc (roczne, kwartalne).</summary>
    NotDue = 5,

    /// <summary>Zlecenie zakończone przed tym miesiącem — nie jest oczekiwane ani liczone w sumie.</summary>
    Ended = 6,
}
