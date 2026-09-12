using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Transactions.Consts;

/// <summary>Kierunek przepływu w filtrze listy transakcji.</summary>
/// <remarks>
/// ⚠️ Konwerter na NAZWY, nie liczby. Aplikacja nie rejestruje globalnie
/// <c>JsonStringEnumConverter</c>, więc bez tego atrybutu enum w CIELE żądania trzeba by
/// podawać liczbą — a front wysyła `"All"`, dokładnie tak jak w query stringu listy.
/// Bez tego akcja „zaznacz wszystkie N pasujących" kończyła się 400: filtr w JSON-ie niósł
/// `direction: "All"`, którego wiązanie nie potrafiło odczytać.
///
/// Atrybut na TYPIE, a nie globalna opcja, bo zmiana globalna dotknęłaby też odpowiedzi
/// innych slice'ów. Tu jest bezpieczna: <see cref="TransactionDirection"/> żyje wyłącznie
/// w <see cref="TransactionFilterRequestDto"/>.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<TransactionDirection>))]
public enum TransactionDirection
{
    All = 1,
    Expense = 2,
    Income = 3,
}
