namespace BudgetTracker.Api.Domain;

/// <summary>
/// Nazwana koperta na koncie oszczędnościowym — „1 800 zł na OC w maju".
/// </summary>
/// <remarks>
/// ⚠️ <b>Uzbierane to suma WPŁAT użytkownika</b> (<see cref="Contributions"/>, zgłoszenie #23), a nie automatyczny
/// rozkład stanu konta. Wcześniej kolejka rozkładała oszczędności od najbliższego terminu, więc rezerwacja
/// świeżo założona przy pełnym koncie od razu pokazywała 100% — cel „osiągnięty”, choć nikt na niego nie odłożył.
///
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
    DateTimeOffset createdAt,
    Guid userId = default) : Entity
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

    /// <summary>Właściciel — powielony z <see cref="Budget.UserId"/>, wyłącznie pod RLS w Postgresie.</summary>
    /// <remarks>Domyślne <c>default</c> z tego samego powodu co <see cref="Budget.UserId"/>.</remarks>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Nazwa koperty — „Ubezpieczenie OC". To ona identyfikuje rezerwację dla człowieka.</summary>
    public string Name { get; protected set; } = name;

    /// <summary>Kwota do uzbierania, zawsze dodatnia. <c>numeric(18,2)</c> jak wszystkie kwoty.</summary>
    public decimal Amount { get; protected set; } = amount;

    /// <summary>
    /// Termin — pierwszy dzień miesiąca, na który pieniądze mają być gotowe.
    /// </summary>
    /// <remarks>
    /// <c>null</c> = „przy okazji” (zakup bez daty) — taka rezerwacja nigdy nie jest „po terminie”.
    /// </remarks>
    public DateOnly? DueMonth { get; protected set; } = dueMonth;

    /// <summary>
    /// Kolejność na liście przy tym samym terminie. Mniejsza liczba = wyżej.
    /// Domyślnie 0 — wtedy o kolejności decyduje <c>Id</c>, czyli moment utworzenia.
    /// </summary>
    public int Priority { get; protected set; } = priority;

    /// <summary>
    /// Kiedy rezerwację rozliczono; <c>null</c> = jeszcze zbiera.
    /// </summary>
    /// <remarks>
    /// Rozliczenie zamyka rezerwację: nie przyjmuje już wpłat i przestaje pomniejszać wolne środki — zapłacony zakup
    /// zszedł już ze stanu konta (zgłoszenie #23; wcześniej koperta była roczna i odejmowała się dalej).
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

    /// <summary>Umowne wpłaty na tę rezerwację, od najstarszej. Ich suma to „uzbierane”.</summary>
    public List<SavingsContribution> Contributions { get; protected set; } = [];

    /// <summary>Suma wpłat.</summary>
    public decimal Contributed => Contributions.Sum(c => c.Amount);

    /// <summary>Wpłata na rezerwację. Czy kwota się mieści (brakująca kwota, stan konta), sprawdza handler.</summary>
    /// <remarks>Nowa lista, nie dopisanie w miejscu: EF porównuje kolumnę JSON jako całość.</remarks>
    public SavingsContribution Contribute(DateOnly date, decimal amount)
    {
        var contribution = new SavingsContribution(Guid.CreateVersion7(), date, amount);
        Contributions = [.. Contributions, contribution];
        return contribution;
    }

    /// <summary>Wycofuje wpłatę; <c>false</c>, gdy takiej wpłaty nie ma.</summary>
    public bool WithdrawContribution(Guid contributionId)
    {
        if (Contributions.All(c => c.Id != contributionId)) return false;
        Contributions = [.. Contributions.Where(c => c.Id != contributionId)];
        return true;
    }

    /// <summary>Kiedy rezerwację założono — do kolejności przy równym terminie i priorytecie.</summary>
    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>Edycja koperty: nazwa, kwota, termin i miejsce na liście.</summary>
    /// <remarks>
    /// ⚠️ Budżetu NIE da się zmienić — przeniesienie rezerwacji między budżetami przesunęłoby
    /// ją do innej puli oszczędności, a wskazana wypłata i wpłaty zostały w starym budżecie.
    /// Kto chce przenieść, zakłada nową.
    /// </remarks>
    public void Update(string name, decimal amount, DateOnly? dueMonth, int priority)
    {
        Name = name;
        Amount = amount;
        DueMonth = dueMonth;
        Priority = priority;
    }

    /// <summary>Zamyka rezerwację wskazaną transakcją — wypłatą z oszczędności albo zakupem zlecenia epizodycznego.</summary>
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

    /// <summary>Cofa rozliczenie — rezerwacja znów przyjmuje wpłaty i pomniejsza wolne środki, z tym samym terminem.</summary>
    public void Unsettle()
    {
        SettledAt = null;
        SettledTransactionBusinessId = null;
    }
}
