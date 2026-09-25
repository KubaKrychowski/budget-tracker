namespace BudgetTracker.Api.Features.Limits.Contracts;

/// <summary>Ekran „Limity wydatków" dla jednego budżetu i jednego miesiąca — wszystko jednym żądaniem.</summary>
/// <param name="Month">Oglądany miesiąc (pierwszy dzień).</param>
/// <param name="CurrentMonth">Bieżący miesiąc — do przycisku „Bieżący miesiąc" i do rozstrzygnięcia <paramref name="ReadOnly"/>.</param>
/// <param name="ReadOnly">
/// Miesiąc zamknięty. Limity zmienia się od bieżącego miesiąca w przód — zmiana wstecz przepisałaby
/// historię, na której ktoś już podjął decyzje.
/// </param>
/// <param name="LimitTotal">Miesięczny limit budżetu = suma limitów obowiązujących w miesiącu.</param>
/// <param name="SpentOutside">
/// Wydatki w kategoriach BEZ limitu. Pilnuje, żeby „limit budżetu" nie udawał wszystkich wydatków.
/// </param>
/// <param name="UncategorizedCount">
/// Wydatki bez kategorii w miesiącu. ⚠️ Nie liczą się do ŻADNEGO limitu (nie ma do czego ich przypisać),
/// więc ekran musi to powiedzieć — inaczej limit wygląda bezpieczniej, niż jest.
/// </param>
/// <param name="HasAnyLimit">
/// Czy budżet ma limit w JAKIMKOLWIEK miesiącu — nie tylko w oglądanym.
///
/// ⚠️ Bez tego pusty miesiąc nie do odróżnienia od pustego budżetu, a to dwie różne wiadomości:
/// „nie ustawiłeś jeszcze żadnego limitu" kontra „w tym miesiącu ich nie ma, ale w innych są".
/// Druga myliła użytkownika, który ustawił limity na przyszły miesiąc i usłyszał, że nie ma żadnych.
/// </param>
public sealed record LimitsResponseDto(
    DateOnly Month,
    DateOnly CurrentMonth,
    bool ReadOnly,
    IReadOnlyList<LimitRowResponseDto> Limits,
    IReadOnlyList<UnlimitedCategoryResponseDto> Unlimited,
    IReadOnlyList<LimitCategoryOptionResponseDto> Categories,
    decimal LimitTotal,
    decimal SpentInLimited,
    decimal SpentOutside,
    int OverLimitCount,
    int UncategorizedCount,
    decimal UncategorizedAmount,
    IReadOnlyList<Guid> SelectedBudgetIds,
    IReadOnlyList<LimitsBudgetOptionResponseDto> Budgets,
    bool HasAnyLimit);
