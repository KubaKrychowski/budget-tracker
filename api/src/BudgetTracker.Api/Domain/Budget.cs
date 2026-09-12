namespace BudgetTracker.Api.Domain;

/// <summary>
/// Etap 1 wg CLAUDE.md §2, wciągnięty do Etapu 0, bo design dashboardu zawiera
/// sekcję „Stan budżetu" i selektor budżetu. UI do ustawiania limitów jeszcze nie istnieje.
/// </summary>
public class Budget(
    string name,
    DateOnly month,
    decimal initialBalance,
    DateTimeOffset createdAt,
    string currency = "PLN") : Entity
{
    /// <summary>Nazwa widoczna w selektorze na dashboardzie (np. „Podstawowy").</summary>
    public string Name { get; protected set; } = name;

    /// <summary>Miesiąc, którego dotyczy budżet — zawsze pierwszy dzień miesiąca.</summary>
    public DateOnly Month { get; protected set; } = month;

    /// <summary>
    /// Kod waluty ISO 4217 — klucz obcy do słownika <see cref="Domain.Currency"/>. W bazie stoi sam kod
    /// (<c>PLN</c>), a nie liczbowy identyfikator, żeby przeglądając tabelę nie trzeba było go sprawdzać.
    /// </summary>
    public string Currency { get; protected set; } = currency;

    /// <summary>
    /// Stan, od którego budżet startuje — punkt odniesienia dla bilansu.
    /// </summary>
    /// <remarks>
    /// Bilans na dany dzień to <c>InitialBalance</c> plus suma transakcji budżetu z datą
    /// nie późniejszą niż ten dzień. Wartość MOŻE być ujemna: debet na koncie jest legalnym
    /// stanem początkowym.
    /// </remarks>
    public decimal InitialBalance { get; protected set; } = initialBalance;

    /// <summary>
    /// Kiedy budżet powstał — kolumna „Data utworzenia" na liście w ustawieniach.
    /// </summary>
    /// <remarks>
    /// ⚠️ Dla wierszy sprzed migracji `BudgetCreatedAtAndDisabledAt` ta data jest NIEPRAWDZIWA:
    /// nie było z czego jej odtworzyć, więc dostały moment wykonania migracji.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>
    /// Kiedy budżet wyłączono; <c>null</c> = aktywny.
    /// </summary>
    /// <remarks>
    /// ⚠️ To NIE jest <see cref="Entity.DeletedAt"/> i pomylenie ich daje błąd, którego nie widać:
    /// budżet albo zniknie, choć miał być tylko wyłączony, albo zostanie widoczny, choć miał być
    /// skasowany. Wyłączony budżet jest w pełni widoczny — filtr globalny go nie dotyczy.
    ///
    /// Znaczenie jest wąskie: wyłączenie zamyka budżet na <b>nowe dane</b> (import, dodawanie
    /// i edycja transakcji), a NIE na zarządzanie nim. Edycja, reset i usunięcie działają tak
    /// samo jak dla aktywnego — inaczej wyłączenie byłoby pułapką, z której trzeba się wygrzebywać
    /// włączeniem z powrotem.
    /// </remarks>
    public DateTimeOffset? DisabledAt { get; protected set; }

    /// <summary>Edycja z ekranu ustawień: nazwa i bilans początkowy.</summary>
    /// <remarks>
    /// Miesiąca i waluty NIE da się zmienić — miesiąc jest częścią tożsamości budżetu (po nim
    /// rozstrzyga się budżet domyślny), a zmiana waluty nie przewalutowałaby istniejących kwot,
    /// więc zmieniłaby znaczenie całej historii.
    /// </remarks>
    public void Update(string name, decimal initialBalance)
    {
        Name = name;
        InitialBalance = initialBalance;
    }

    /// <summary>Zamyka budżet na nowe dane. Zarządzanie (edycja, reset, usunięcie) działa dalej.</summary>
    public void Disable(DateTimeOffset at) => DisabledAt = at;

    /// <summary>Otwiera budżet z powrotem na nowe dane.</summary>
    public void Enable() => DisabledAt = null;
}
