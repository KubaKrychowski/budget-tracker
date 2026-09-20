using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>
/// Reguła kategoryzacji: wzorzec w opisie → kategoria. Bootstrap dla zimnego startu ML
/// (CLAUDE.md §3) i trwałe źródło pewnych dopasowań, których model nie musi się uczyć.
///
/// Trzymana w bazie, nie w kodzie, żeby dołożenie sprzedawcy nie wymagało wdrożenia.
/// </summary>
public class CategoryRule(
    int categoryId,
    RuleDirection direction,
    int priority,
    Guid userId,
    string? pattern = null,
    string? transactionTypePattern = null,
    decimal? minAmount = null,
    decimal? maxAmount = null,
    string? note = null) : Entity
{
    /// <summary>
    /// Wyrażenie regularne dopasowywane do ZNORMALIZOWANEGO opisu
    /// (patrz <c>DescriptionNormalizer</c>) — czyli do tekstu bez cyfr, dat i interpunkcji.
    ///
    /// <c>null</c> = reguła nie patrzy na opis; wtedy musi być ustawiony
    /// <see cref="TransactionTypePattern"/>, inaczej łapałaby wszystko.
    /// </summary>
    public string? Pattern { get; protected set; } = pattern;

    /// <summary>
    /// Opcjonalne wyrażenie dopasowywane do TYPU operacji z wyciągu (np. „Wypłata w bankomacie”).
    /// Potrzebne, bo są transakcje, których opis nie niesie żadnej informacji: przy wypłacie
    /// z bankomatu opisem jest sam adres, nie do odróżnienia tekstowo od zakupu. Tylko typ
    /// operacji mówi, co to naprawdę było.
    ///
    /// <c>null</c> = reguła nie patrzy na typ. Jeśli ustawione razem z <see cref="Pattern"/>,
    /// muszą pasować OBA.
    /// </summary>
    public string? TransactionTypePattern { get; protected set; } = transactionTypePattern;

    /// <summary>
    /// Strona przepływu, po której reguła obowiązuje.
    /// Chroni przed dopasowaniem reguły wydatkowej do wpływu i odwrotnie.
    /// </summary>
    public RuleDirection Direction { get; protected set; } = direction;

    public int CategoryId { get; protected set; } = categoryId;

    /// <summary>
    /// Kolejność sprawdzania, rosnąco. Potrzebna, bo wzorce nachodzą na siebie:
    /// „ubezpieczeniowy fundusz” (kara) musi wygrać z „ubezpiecz” (składka),
    /// inaczej mandat wyląduje jako ubezpieczenie.
    /// </summary>
    public int Priority { get; protected set; } = priority;

    /// <summary>
    /// Opcjonalne widełki kwoty (wartość bezwzględna). Wynikły z realnych danych:
    /// zakup na stacji paliw poniżej 50 zł to sklep, nie tankowanie — kawa za kilkanaście złotych
    /// nie jest paliwem. Sama nazwa sprzedawcy tego nie rozstrzyga, dopiero kwota.
    /// </summary>
    public decimal? MinAmount { get; protected set; } = minAmount;

    /// <inheritdoc cref="MinAmount"/>
    public decimal? MaxAmount { get; protected set; } = maxAmount;

    /// <summary>Opis dla człowieka — po co ta reguła istnieje. Widoczny przy edycji reguł.</summary>
    public string? Note { get; protected set; } = note;

    /// <summary>
    /// Właściciel reguły: identyfikator konta (<c>sub</c> z tokenu), tak samo jak <c>UserId</c> budżetu i jego dzieci.
    /// <see cref="SharedUserId"/> oznacza regułę WSPÓLNĄ (bazową): widzą ją wszyscy, ale nikt jej nie zmieni ani nie skasuje.
    /// </summary>
    /// <remarks>
    /// ⚠️ Odczyt i zapis chroni RLS w Postgresie (migracja <c>AddRuleOwnerAndRls</c>), tak samo jak filtr Owner w EF.
    /// Reguły bazowe zakłada seed uruchamiany jako <c>budget_jobs</c> — zwykły użytkownik ma prawo tylko do własnych.
    /// </remarks>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Wartość <see cref="UserId"/> reguły wspólnej (bazowej) — pusty identyfikator, żaden użytkownik go nie ma.</summary>
    public static readonly Guid SharedUserId = Guid.Empty;

    /// <summary>Reguła wspólna: tylko do odczytu dla użytkowników.</summary>
    public bool IsShared => UserId == SharedUserId;

    /// <summary>Edycja reguły w CAŁOŚCI — tak samo jak ją tworzono.</summary>
    /// <remarks>
    /// Jedna metoda na wszystkie pola, bo reguła jest spójna tylko jako komplet: wzorzec bez
    /// kierunku albo widełki bez wzorca to reguła, która zapisze się i nigdy nie zadziała.
    /// Walidacją tego kompletu zajmuje się <c>CategoryRuleValidator</c> przed zapisem.
    /// </remarks>
    public void Update(
        int categoryId,
        RuleDirection direction,
        int priority,
        string? pattern,
        string? transactionTypePattern,
        decimal? minAmount,
        decimal? maxAmount,
        string? note)
    {
        CategoryId = categoryId;
        Direction = direction;
        Priority = priority;
        Pattern = pattern;
        TransactionTypePattern = transactionTypePattern;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Note = note;
    }
}
