namespace BudgetTracker.Api.Domain;

/// <summary>
/// Wspólna baza wszystkich encji: klucz zapisu, klucz publiczny i znacznik skasowania.
/// </summary>
/// <remarks>
/// <b>Dwa identyfikatory, nie jeden.</b> <see cref="Id"/> to klucz główny i obcy — służy indeksom
/// i relacjom, nigdy nie wychodzi z API. <see cref="BusinessId"/> jest kluczem publicznym: to na
/// nim operują kontrakty, adresy i front, i to on ma być stabilny przy re-seedzie i przenoszeniu
/// danych między środowiskami.
///
/// <b>Stan zmienia się metodami, nie przypisaniem.</b> Wszystkie właściwości mają
/// <c>protected set</c>, więc encja nie ma jak wpaść w stan, którego nikt nie nazwał. EF zapisuje
/// je refleksją, a przy materializacji używa konstruktora encji (wiąże parametry po nazwach
/// właściwości) — dlatego nazwy parametrów konstruktorów NIE są dowolne.
///
/// ⚠️ Klasa jest bazą CZYSTO KODOWĄ, nie encją. EF nie widzi jej w modelu (nie ma własnego
/// <c>DbSet</c> i nic na nią nie wskazuje nawigacją), więc nie powstaje z niej hierarchia
/// dziedziczenia TPH — właściwości są po prostu mapowane na każdą tabelę osobno.
/// Gdyby ktoś kiedyś dodał nawigację wskazującą na <c>Entity</c>, EF zacznie ją mapować
/// i wszystkie tabele wpadną do jednej. Nie rób tego.
/// </remarks>
public abstract class Entity
{
    /// <summary>Klucz główny. Wewnętrzny — nie pokazuj go na zewnątrz.</summary>
    public int Id { get; protected set; }

    /// <summary>
    /// Publiczny identyfikator, nadawany w momencie utworzenia obiektu — nie przy zapisie.
    /// Dzięki temu da się go użyć, zanim encja trafi do bazy.
    /// </summary>
    /// <remarks>
    /// <c>CreateVersion7()</c>, nie <c>NewGuid()</c>: v7 jest uporządkowany w czasie, więc unikalny
    /// indeks rośnie na końcu B-drzewa zamiast fragmentować się losowo po całej strukturze.
    ///
    /// Encje wczytywane z bazy też przechodzą przez ten inicjalizator, a wartość z bazy go
    /// nadpisuje. Kosztuje to jedno wygenerowanie Guida na wiersz — przy skali tego projektu
    /// nieistotne, a cena jest za to, że nie ma encji bez identyfikatora.
    /// </remarks>
    public Guid BusinessId { get; protected set; } = Guid.CreateVersion7();

    /// <summary>
    /// Znacznik skasowania logicznego (<c>timestamptz</c>). <c>null</c> = żywa.
    /// </summary>
    /// <remarks>
    /// Globalny filtr w <c>AppDbContext</c> odsiewa skasowane automatycznie — żeby je zobaczyć,
    /// trzeba jawnie poprosić przez <c>IgnoreQueryFilters()</c>.
    ///
    /// ⚠️ Filtr jest CICHY: zapytanie, które miało zobaczyć skasowane, zwróci pustkę zamiast
    /// błędu. Przy diagnostyce sprawdzaj to w pierwszej kolejności.
    /// </remarks>
    public DateTimeOffset? DeletedAt { get; protected set; }

    /// <summary>Stempluje encję jako skasowaną logicznie.</summary>
    /// <remarks>
    /// Wołane przez <c>SoftDeleteInterceptor</c> (zamiast fizycznego <c>DELETE</c>) i przez handlery,
    /// które kasują dzieci razem z rodzicem. ⚠️ Znacznik musi być dla rodzica i dzieci TEN SAM —
    /// przywracanie dopasowuje je po równości tej daty, więc drugie odczytanie zegara rozjeżdża
    /// kasowanie z przywracaniem.
    /// </remarks>
    public void MarkDeleted(DateTimeOffset at) => DeletedAt = at;

    /// <summary>Zdejmuje stempel skasowania.</summary>
    public void Restore() => DeletedAt = null;

    /// <summary>
    /// Podmienia identyfikator publiczny na z góry ustalony — WYŁĄCZNIE dla danych seedowanych.
    /// </summary>
    /// <remarks>
    /// ⚠️ Jedyna droga, którą <see cref="BusinessId"/> da się zmienić po utworzeniu, i istnieje
    /// tylko dla seedów: <c>DeterministicGuid.For(nazwa)</c> musi dać ten sam identyfikator po
    /// re-seedzie i na innym środowisku (CLAUDE.md §4), więc Guid z inicjalizatora trzeba nadpisać.
    /// Kod aplikacyjny tego nie woła — tam każda encja dostaje świeży Guid v7 przy <c>new</c>.
    /// </remarks>
    public TEntity WithSeedBusinessId<TEntity>(Guid businessId)
        where TEntity : Entity
    {
        BusinessId = businessId;
        return (TEntity)this;
    }
}
