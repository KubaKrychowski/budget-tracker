namespace BudgetTracker.Api.Domain;

/// <summary>
/// Zlecenie epizodyczne — większy, nieregularny wydatek: zaplanowany („Nowy laptop do marca”) albo już zrealizowany
/// („Serwis auta”).
/// </summary>
/// <remarks>
/// <para>
/// Zastępuje flagę „duży wydatek” na transakcji. Zrealizowane zlecenie wskazuje transakcję
/// (<see cref="TransactionBusinessId"/>) i to ona jest jedynym źródłem kwoty, daty i kategorii — zlecenie ich nie
/// powiela, więc nie mogą się rozjechać. Zaplanowane niesie własny plan (<see cref="PlannedAmount"/>,
/// <see cref="DueMonth"/>, <see cref="CategoryId"/>), bo transakcji jeszcze nie ma.
/// </para>
/// <para>
/// ⚠️ Plan ZOSTAJE po realizacji. Po nim poznajemy zlecenie, które było zaplanowane — tylko takie da się „cofnąć do
/// zaplanowanych”; zlecenie oznaczone wprost z listy transakcji nie ma dokąd wrócić.
/// </para>
/// <para>
/// Rezerwacja (<see cref="ReservationBusinessId"/>) to ta sama koperta co na ekranie rezerwacji — zlecenie jej nie
/// kopiuje, tylko nią rządzi: nazwa, kwota i termin zmieniają się razem ze zleceniem. Zlecenie jest zasadą budżetu,
/// jak zlecenie stałe — reset je zostawia, usunięcie budżetu zabiera.
/// </para>
/// </remarks>
public class EpisodicOrder(
    Guid budgetBusinessId,
    string name,
    string? description,
    DateTimeOffset createdAt) : Entity
{
    /// <summary>Budżet zlecenia — zwykła kolumna z publicznym identyfikatorem, bez relacji EF (jak przy transakcji).</summary>
    public Guid BudgetBusinessId { get; protected set; } = budgetBusinessId;

    public string Name { get; protected set; } = name;

    /// <summary>Opcjonalny, wieloliniowy opis — widoczny pod nazwą w tabeli.</summary>
    public string? Description { get; protected set; } = description;

    /// <summary>Kategoria planu; <c>null</c> przy zleceniu, które nigdy nie było zaplanowane.</summary>
    /// <remarks>Po realizacji ekran pokazuje kategorię TRANSAKCJI — to ona trafia do limitów i dashboardu.</remarks>
    public int? CategoryId { get; protected set; }

    /// <summary>Kwota planu, dodatnia; <c>null</c> = zlecenie nie było zaplanowane.</summary>
    public decimal? PlannedAmount { get; protected set; }

    /// <summary>Pierwszy dzień miesiąca terminu planu; <c>null</c> = zlecenie nie było zaplanowane.</summary>
    public DateOnly? DueMonth { get; protected set; }

    /// <summary>Transakcja, którą zlecenie zrealizowano; <c>null</c> = jeszcze zaplanowane.</summary>
    /// <remarks>Publiczny identyfikator bez relacji EF. Jedna transakcja należy do jednego zlecenia (indeks unikalny).</remarks>
    public Guid? TransactionBusinessId { get; protected set; }

    /// <summary>Rezerwacja założona z „Załóż cel oszczędzania”; <c>null</c> = bez oszczędzania.</summary>
    public Guid? ReservationBusinessId { get; protected set; }

    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    public bool IsRealized => TransactionBusinessId is not null;

    public bool WasPlanned => DueMonth is not null;

    /// <summary>Nazwa i opis — jedyne, co da się zmienić w zleceniu zrealizowanym.</summary>
    public void Rename(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>Plan zakupu — kategoria, kwota i termin ruszają się razem.</summary>
    public void Plan(int categoryId, decimal amount, DateOnly dueMonth)
    {
        CategoryId = categoryId;
        PlannedAmount = amount;
        DueMonth = new DateOnly(dueMonth.Year, dueMonth.Month, 1);
    }

    /// <summary>Realizacja wskazaną transakcją. Czy transakcja się nadaje, sprawdza handler.</summary>
    public void Realize(Guid transactionBusinessId) => TransactionBusinessId = transactionBusinessId;

    /// <summary>Powrót do zaplanowanych — plan został, więc nie ma czego odtwarzać.</summary>
    public void Unrealize() => TransactionBusinessId = null;

    public void AttachReservation(Guid reservationBusinessId) => ReservationBusinessId = reservationBusinessId;
}
