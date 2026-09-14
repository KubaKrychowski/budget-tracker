using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Jak często schodzi zlecenie stałe.</summary>
/// <remarks>
/// Do bazy trafia NAZWA wartości — kod słownika <see cref="StandingOrderRhythmDictionary"/>.
/// ⚠️ Zmiana nazwy wymaga migracji danych (CLAUDE.md §5). Konwerter na nazwy, bo enum jedzie przez JSON
/// w kontrakcie zapisu, a aplikacja nie rejestruje konwertera globalnie.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StandingOrderRhythm>))]
public enum StandingOrderRhythm
{
    /// <summary>Co miesiąc — czynsz, kredyt, abonament.</summary>
    Monthly = 1,

    /// <summary>Co trzy miesiące, licząc od miesiąca wskazanego w zleceniu.</summary>
    Quarterly = 2,

    /// <summary>Raz w roku, we wskazanym miesiącu — np. polisa.</summary>
    Yearly = 3,
}
