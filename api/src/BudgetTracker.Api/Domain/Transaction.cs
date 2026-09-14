using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Jedna operacja z wyciągu albo dodana ręcznie — podstawowa jednostka danych w aplikacji.</summary>
public class Transaction(
    DateOnly date,
    decimal amount,
    string description,
    DateTimeOffset createdAt,
    TransactionStatus status,
    int? categoryId = null,
    decimal? confidence = null,
    bool isLargeExpense = false,
    string transactionType = "",
    string? externalReference = null,
    Guid? budgetBusinessId = null,
    int? accountId = null,
    int? importBatchId = null) : Entity
{
    /// <summary>Data księgowania. DateOnly → kolumna `date`, bez strefy czasowej.</summary>
    public DateOnly Date { get; protected set; } = date;

    /// <summary>
    /// KONWENCJA ZNAKU: wartość ujemna = wydatek, dodatnia = przychód.
    /// </summary>
    /// <remarks>
    /// Dashboard rozdziela na tej podstawie karty „suma wydatków" i „suma przychodów",
    /// a CLAUDE.md §3 wymienia znak jako cechę modelu ML. Nie zapisuj kwot bezwzględnych.
    /// </remarks>
    public decimal Amount { get; protected set; } = amount;

    public string Description { get; protected set; } = description;

    /// <summary>Flaga „duży wydatek" — proces 4 z Etapu 0, nie osobny ekran.</summary>
    public bool IsLargeExpense { get; protected set; } = isLargeExpense;

    /// <summary>
    /// Klucz obcy do <see cref="Domain.Category"/> — bez nawigacji (CLAUDE.md §5). Nazwę kategorii
    /// dociąga zapytanie złączeniem z <c>Categories</c>.
    /// </summary>
    public int? CategoryId { get; protected set; } = categoryId;

    public int? AccountId { get; protected set; } = accountId;

    /// <summary>Pewność predykcji ML (0–1). Null, gdy kategorię nadał człowiek lub reguła.</summary>
    public decimal? Confidence { get; protected set; } = confidence;

    public TransactionStatus Status { get; protected set; } = status;

    /// <summary>Kiedy rekord trafił do bazy — timestamptz, w przeciwieństwie do <see cref="Date"/>.</summary>
    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>
    /// Typ operacji z wyciągu („Płatność kartą", „Wypłata w bankomacie", „Przelew z rachunku").
    /// </summary>
    /// <remarks>
    /// Cecha kategoryzacji, której CLAUDE.md §3 nie wymienia, a która na realnych danych
    /// okazała się mocniejsza od opisu: wszystkie wypłaty gotówki mają w opisie sam adres
    /// bankomatu i są nierozpoznawalne tekstowo, ale ich typ identyfikuje je bezbłędnie.
    /// Pusty dla transakcji dodanych ręcznie.
    /// </remarks>
    public string TransactionType { get; protected set; } = transactionType;

    /// <summary>
    /// Numer referencyjny z wyciągu — pierwszy człon klucza deduplikacji.
    /// Null dla transakcji dodanych ręcznie i dla tych wyciągów, które go nie podają
    /// (w próbce PKO miało go 75,8% wierszy, więc fallback na kwotę + datę + opis jest konieczny).
    /// </summary>
    public string? ExternalReference { get; protected set; } = externalReference;

    /// <summary>
    /// Budżet, na który zaksięgowano transakcję — wybierany w kroku 1 importu
    /// („Budżet na który zostaną zapisane zmiany" z makiety).
    /// </summary>
    /// <remarks>
    /// ⚠️ ŚWIADOMIE bez relacji EF (bez FK, bez nawigacji <c>Budget</c>) — to zwykła kolumna
    /// z publicznym identyfikatorem budżetu, którą łączy się z <see cref="Budget.BusinessId"/>
    /// wyłącznie w kodzie zapytań, nigdy przez klucz obcy w bazie. Transakcja to realne dane
    /// z wyciągu; budżet jest tylko ramą, na którą się je zaksięgowuje — związek między nimi
    /// jest miękki z założenia, nie tylko z powodu <c>DeleteBehavior.Restrict</c>.
    ///
    /// Budżet był dotąd wyłącznie zestawem miesięcznych limitów per kategoria
    /// (<see cref="BudgetItem"/>), bez związku z pojedynczą transakcją. To pole powstało
    /// z makiety importu i jest świadomą decyzją użytkownika, nie wnioskiem z modelu
    /// z CLAUDE.md §5 — tam relacji Budżet→Transakcja nie ma.
    ///
    /// Null dla transakcji sprzed wprowadzenia pola i dla dodanych ręcznie.
    /// </remarks>
    public Guid? BudgetBusinessId { get; protected set; } = budgetBusinessId;

    /// <summary>Null dla transakcji dodanych ręcznie — te nie pochodzą z żadnego importu.</summary>
    public int? ImportBatchId { get; protected set; } = importBatchId;

    /// <summary>Zlecenie stałe, do którego przypięła transakcję jego reguła; <c>null</c> = żadne.</summary>
    /// <remarks>Zwykła kolumna z publicznym identyfikatorem, bez relacji EF. Przypięcie NIE zmienia kategorii.</remarks>
    public Guid? StandingOrderBusinessId { get; protected set; }

    /// <summary>
    /// Zlecenie, od którego użytkownik RĘCZNIE odpiął transakcję.
    /// </summary>
    /// <remarks>
    /// ⚠️ Bez tej pamięci każde ponowne dopasowanie (zmiana reguły, kolejny import) przypinałoby transakcję z powrotem
    /// i „Odepnij” działałoby do pierwszej zmiany. Pamiętamy KONKRETNE zlecenie, a nie ogólny zakaz: transakcja odpięta
    /// od „Czynszu” wciąż może pasować do innego zlecenia.
    /// </remarks>
    public Guid? StandingOrderUnpinnedFrom { get; protected set; }

    /// <summary>Ręczne odpięcie — transakcja nie wróci do tego zlecenia przy ponownym dopasowaniu.</summary>
    public void UnpinFromStandingOrder()
    {
        StandingOrderUnpinnedFrom = StandingOrderBusinessId;
        StandingOrderBusinessId = null;
    }

    /// <summary>Edycja inline z listy transakcji — pola, które widzi użytkownik w wierszu.</summary>
    /// <remarks>
    /// Kategoria NIE jest tu ustawiana, choć edytuje się ją w tym samym wierszu: jej zmiana
    /// pociąga za sobą status i pewność, więc ma własną metodę (<see cref="Recategorize"/>).
    /// </remarks>
    public void Edit(DateOnly date, string description, decimal amount, bool isLargeExpense)
    {
        Date = date;
        Description = description;
        Amount = amount;
        IsLargeExpense = isLargeExpense;
    }

    /// <summary>Zmiana kategorii razem ze statusem i pewnością — trzy pola, które muszą ruszać się RAZEM.</summary>
    /// <remarks>
    /// ⚠️ Rozjazd między nimi jest niewidoczny na ekranie i trudny do odtworzenia: wiersz
    /// z kategorią nadaną przez człowieka, ale z pewnością modelu, wraca do kolejki przeglądu;
    /// wiersz bez kategorii ze statusem <see cref="TransactionStatus.AutoCategorized"/> nigdy
    /// do niej nie trafi. Dlatego encja przyjmuje całą trójkę naraz.
    ///
    /// O tym, CO wpisać, decyduje handler: korekta człowieka bierze status i pewność
    /// z <c>ManualCategoryCorrection</c>, a przeliczenie modelem — z progu pewności. Encja nie zna
    /// progu ani tego, kto podjął decyzję.
    /// </remarks>
    public void Recategorize(int? categoryId, TransactionStatus status, decimal? confidence)
    {
        CategoryId = categoryId;
        Status = status;
        Confidence = confidence;
    }
}
