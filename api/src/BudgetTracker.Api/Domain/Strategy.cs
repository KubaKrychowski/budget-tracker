namespace BudgetTracker.Api.Domain;

/// <summary>
/// Strategia — nazwany graf kafelków (zdarzeń, akcji i warunków) w jednym budżecie, z którego symulator liczy
/// gotówkę i dług miesiąc po miesiącu.
/// </summary>
/// <remarks>
/// <para>
/// Zasada budżetu jak cele i zlecenia: <b>reset ją zostawia, usunięcie zabiera</b>. Węzły i połączenia siedzą w
/// kolumnach <c>jsonb</c> — to wartości strategii bez własnego cyklu życia, więc kasują się i przywracają razem z nią.
/// </para>
/// <para>
/// Strategia jest PLANEM, nie danymi z wyciągu: nic w niej nie zmienia budżetu. Liczby (stan początkowy, kredyt)
/// wpisuje użytkownik — aplikacja nie ma encji kredytu.
/// </para>
/// </remarks>
public class Strategy(
    Guid budgetBusinessId,
    string name,
    DateOnly startMonth,
    decimal startCash,
    int horizonMonths,
    DateTimeOffset createdAt,
    Guid userId = default) : Entity
{
    /// <summary>Najkrótszy horyzont symulacji w miesiącach.</summary>
    public const int MinHorizonMonths = 6;

    /// <summary>Ile wariantów (poza bazowym) może mieć strategia — pasek wariantów i porównanie muszą się mieścić na ekranie.</summary>
    public const int MaxVariants = 10;

    /// <summary>Najdłuższy horyzont symulacji — 5 lat wystarcza na kredyt konsumpcyjny i trzyma serię w ryzach.</summary>
    public const int MaxHorizonMonths = 60;

    /// <summary>Budżet strategii — zwykła kolumna z publicznym identyfikatorem, bez relacji EF (jak przy zleceniu).</summary>
    public Guid BudgetBusinessId { get; protected set; } = budgetBusinessId;

    /// <summary>Właściciel — powielony z <see cref="Budget.UserId"/>, wyłącznie pod RLS w Postgresie.</summary>
    /// <remarks>Domyślne <c>default</c> z tego samego powodu co <see cref="Budget.UserId"/>.</remarks>
    public Guid UserId { get; protected set; } = userId;

    public string Name { get; protected set; } = name;

    /// <summary>Pierwszy miesiąc symulacji (klucz okresu — pierwszy dzień miesiąca).</summary>
    public DateOnly StartMonth { get; protected set; } = startMonth;

    /// <summary>Gotówka (oszczędności) na początku pierwszego miesiąca — wpisana przez użytkownika.</summary>
    public decimal StartCash { get; protected set; } = startCash;

    /// <summary>Ile miesięcy liczyć od <see cref="StartMonth"/>.</summary>
    public int HorizonMonths { get; protected set; } = horizonMonths;

    /// <summary>Kafelki tablicy.</summary>
    public List<StrategyNode> Nodes { get; protected set; } = [];

    /// <summary>Połączenia między kafelkami.</summary>
    public List<StrategyEdge> Edges { get; protected set; } = [];

    /// <summary>Warianty — wyłączenia kafelków; „Bazowy” (bez wyłączeń) nie jest zapisywany, istnieje zawsze.</summary>
    public List<StrategyVariant> Variants { get; protected set; } = [];

    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>Ostatni zapis tablicy albo parametrów — do listy „zmieniona …”.</summary>
    public DateTimeOffset UpdatedAt { get; protected set; } = createdAt;

    /// <summary>Zapis całej strategii: parametry i graf zmieniają się razem, bo razem decydują o wyniku.</summary>
    /// <remarks>Nowe listy, nie edycja w miejscu: EF porównuje kolumnę JSON jako całość.</remarks>
    public void Replace(
        string name,
        DateOnly startMonth,
        decimal startCash,
        int horizonMonths,
        IReadOnlyCollection<StrategyNode> nodes,
        IReadOnlyCollection<StrategyEdge> edges,
        IReadOnlyCollection<StrategyVariant> variants,
        DateTimeOffset now)
    {
        Name = name;
        StartMonth = startMonth;
        StartCash = startCash;
        HorizonMonths = horizonMonths;
        Nodes = [.. nodes];
        Edges = [.. edges];
        Variants = [.. variants];
        UpdatedAt = now;
    }
}
