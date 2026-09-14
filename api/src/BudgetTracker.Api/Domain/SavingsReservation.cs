namespace BudgetTracker.Api.Domain;

/// <summary>
/// Nazwana koperta na koncie oszczędnościowym — „1 800 zł na OC w maju".
/// </summary>
/// <remarks>
/// ⚠️ <b>Rezerwacja ma DWA niezależne postępy: uzbierane i rozliczone.</b> Opłacone
/// ubezpieczenie to rezerwacja <b>zamknięta</b>, a nie „0% zostało". Jedna liczba na
/// pierścieniu tego nie udźwignie, stąd wypełnienie (uzbierane) osobno od wygaszenia
/// (rozliczone).
///
/// ⚠️ <b>Rezerwacja to nie to samo co budżet ani kategoria.</b> Kusi, żeby dowiązać ją do
/// kategorii („Ubezpieczenia") — świadomie tego nie robimy, bo to trzecia relacja na tym
/// samym ekranie i nie wiadomo, czy komukolwiek potrzebna.
/// </remarks>
public class SavingsReservation(
    Guid budgetBusinessId,
    string name,
    decimal amount,
    DateOnly? dueMonth,
    int priority,
    DateTimeOffset createdAt) : Entity
{
    /// <summary>
    /// Budżet, którego dotyczy rezerwacja — publiczny identyfikator, tak samo jak
    /// w <see cref="SavingsGoal.BudgetBusinessId"/> i <see cref="Transaction.BudgetBusinessId"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Świadomie BEZ relacji EF: zwykła kolumna łączona z <see cref="Budget.BusinessId"/>
    /// w kodzie zapytań, nigdy kluczem obcym.
    /// </remarks>
    public Guid BudgetBusinessId { get; protected set; } = budgetBusinessId;

    /// <summary>Nazwa koperty — „Ubezpieczenie OC". To ona identyfikuje rezerwację dla człowieka.</summary>
    public string Name { get; protected set; } = name;

    /// <summary>Kwota do uzbierania, zawsze dodatnia. <c>numeric(18,2)</c> jak wszystkie kwoty.</summary>
    public decimal Amount { get; protected set; } = amount;

    /// <summary>
    /// Termin — pierwszy dzień miesiąca, na który pieniądze mają być gotowe.
    /// </summary>
    /// <remarks>
    /// <c>null</c> = „przy okazji” (zakup bez daty z listy zleceń epizodycznych). ⚠️ Taka rezerwacja zbiera NA KOŃCU
    /// kolejki — po wszystkich z terminem — i nigdy nie jest „po terminie”. Bez tej reguły zakup „kiedyś” odbierałby
    /// pieniądze rachunkowi, który ma przyjść w maju.
    /// </remarks>
    public DateOnly? DueMonth { get; protected set; } = dueMonth;

    /// <summary>
    /// Rozstrzyga kolejkę przy tym samym terminie. Mniejsza liczba = wcześniej w kolejce.
    /// Domyślnie 0 — wtedy o kolejności decyduje <c>Id</c>, czyli moment utworzenia.
    /// </summary>
    public int Priority { get; protected set; } = priority;

    /// <summary>
    /// Kiedy rezerwację rozliczono; <c>null</c> = jeszcze zbiera.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Rozliczenie NIE zwalnia wolnych środków</b> — karta nazywa się „Rezerwacje na ten
    /// rok", więc koperta jest ROCZNA: 5 000 zł zostaje zaklepane niezależnie od tego, ile już
    /// wypłacono. Rozliczenie zamyka rezerwację i zdejmuje ją z kolejki zbierania, i tyle.
    /// Pierwszy odruch przy refaktorze będzie taki, żeby „naprawić" tę liczbę — nie rób tego,
    /// to decyzja użytkownika, a nie przeoczenie (issue #11).
    /// </remarks>
    public DateTimeOffset? SettledAt { get; protected set; }

    /// <summary>
    /// Realna wypłata z oszczędności, która tę rezerwację zamknęła.
    /// </summary>
    /// <remarks>
    /// ⚠️ Bez wskazania transakcji rezerwacje są listą życzeń, która nigdy nie spotyka się
    /// z wykonaniem. Dlatego rozliczenie zawsze wskazuje wiersz z wyciągu, a nie samo
    /// „odhacz jako zrobione".
    ///
    /// Publiczny identyfikator transakcji, znów bez relacji EF — z tego samego powodu
    /// co <see cref="BudgetBusinessId"/>.
    /// </remarks>
    public Guid? SettledTransactionBusinessId { get; protected set; }

    /// <summary>Kiedy rezerwację założono — do kolejności przy równym terminie i priorytecie.</summary>
    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>Edycja koperty: nazwa, kwota, termin i miejsce w kolejce.</summary>
    /// <remarks>
    /// ⚠️ Budżetu NIE da się zmienić — przeniesienie rezerwacji między budżetami przesunęłoby
    /// ją do innej puli i innej kolejki naraz, a wskazana wypłata została w starym budżecie.
    /// Kto chce przenieść, zakłada nową.
    /// </remarks>
    public void Update(string name, decimal amount, DateOnly? dueMonth, int priority)
    {
        Name = name;
        Amount = amount;
        DueMonth = dueMonth;
        Priority = priority;
    }

    /// <summary>Zamyka rezerwację wskazaną wypłatą z oszczędności.</summary>
    /// <remarks>
    /// Dwa pola ruszają się razem, bo „rozliczona" bez wskazanej transakcji to dokładnie ta lista
    /// życzeń, której ten mechanizm ma nie być. Czy transakcja się nadaje, sprawdza handler —
    /// encja nie zna kategorii „Oszczędności" ani pozostałych rezerwacji.
    /// </remarks>
    public void Settle(DateTimeOffset at, Guid transactionBusinessId)
    {
        SettledAt = at;
        SettledTransactionBusinessId = transactionBusinessId;
    }

    /// <summary>Cofa rozliczenie — rezerwacja wraca do kolejki zbierania z tym samym terminem.</summary>
    public void Unsettle()
    {
        SettledAt = null;
        SettledTransactionBusinessId = null;
    }
}
