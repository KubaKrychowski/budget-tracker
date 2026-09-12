using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>
/// Strona przepływu, po której reguła w ogóle ma prawo zadziałać.
///
/// Powstała z realnego błędu: wszystkie kategorie bazowe są wydatkowe, więc wpływy
/// (wynagrodzenie, zwroty, przelewy od ludzi) nie miały dokąd trafić i model wciskał je
/// w kategorię wydatku — wynagrodzenie lądowało w „Gastronomii” z pewnością bliską 1.0,
/// więc nawet nie szło do przeglądu.
///
/// Do bazy trafia NAZWA wartości — kod słownika <see cref="RuleDirectionDictionary"/>.
/// ⚠️ Zmiana nazwy wymaga migracji danych (CLAUDE.md §5).
/// </summary>
/// <remarks>
/// ⚠️ Konwerter na NAZWY, tak jak przy <c>TransactionStatus</c> i <c>MonthVerdict</c>: w kontrakcie reguł
/// (<c>CategoryRuleRequestDto</c>) ten enum jedzie przez JSON, a aplikacja nie rejestruje konwertera globalnie.
/// Bez atrybutu klient musiałby podawać LICZBĘ — a numeracja enumów jest szczegółem implementacji i już raz
/// się zmieniła (przy przejściu na numerację od 1). Nazwa jest kontraktem, liczba nigdy nim nie była.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<RuleDirection>))]
public enum RuleDirection
{
    /// <summary>Bez ograniczenia — reguła dotyczy i wpływów, i wydatków.</summary>
    Any = 1,

    /// <summary>Tylko kwoty ujemne.</summary>
    Expense = 2,

    /// <summary>Tylko kwoty dodatnie.</summary>
    Income = 3,
}
