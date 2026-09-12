namespace BudgetTracker.Api.Domain.Consts;

/// <summary>
/// Cykl życia transakcji. Stan mapuje się wprost na wygląd wiersza w tabeli,
/// więc nie dodawaj wartości bez decyzji o tym, jak mają wyglądać.
///
/// Do bazy trafia NAZWA wartości — kod słownika <see cref="TransactionStatusDictionary"/>, z kluczem obcym.
/// Numeracja nie ma znaczenia, ale zmiana nazwy wymaga migracji danych (przepisania kodów w kolumnie
/// i w słowniku).
///
/// ⚠️ W JSON-ie też idą jako NAZWY — atrybut niżej dotyczy wyłącznie serializacji (EF go nie widzi),
/// zapis w bazie ustawia konwersja w <c>AppDbContext</c>. Front operuje nazwami wszędzie:
/// w query stringu listy, w filtrze akcji masowych i w odpowiedziach, gdzie status i tak
/// wychodzi przez `ToString()`.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(
    typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TransactionStatus>))]
public enum TransactionStatus
{
    /// <summary>Wczytana z CSV, jeszcze nieskategoryzowana.</summary>
    Imported = 1,

    /// <summary>Pewność modelu ≥ progu (0.7) — kategoria nadana automatycznie.</summary>
    AutoCategorized = 2,

    /// <summary>Pewność poniżej progu — czeka na korektę użytkownika.</summary>
    PendingReview = 3,

    /// <summary>Użytkownik ustawił kategorię ręcznie; to zasila douczanie modelu.</summary>
    ManuallyCategorized = 4,

    /// <summary>Użytkownik potwierdził kategorię jako ostateczną.</summary>
    Confirmed = 5
}
