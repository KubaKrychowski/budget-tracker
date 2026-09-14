namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Strona listy transakcji razem z kaflami i zasięgiem budżetów, na którym ją policzono.</summary>
/// <param name="Summary">
/// Agregaty PO filtrach, dla CAŁEGO pasującego zestawu — niezależne od bieżącej strony.
/// </param>
/// <param name="SelectedBudgetIds">
/// Budżety, na których FAKTYCZNIE policzono odpowiedź — wybrane przez użytkownika albo jeden
/// domyślny, gdy nie wybrał żadnego. PUSTA lista tylko wtedy, gdy w bazie nie ma ani jednego
/// budżetu — to pierwszy z trzech pustych stanów.
///
/// Front mirroruje to pole zamiast odtwarzać regułę wyboru u siebie (ta sama zasada co
/// w <c>DashboardResponseDto.SelectedBudgetId</c>).
/// </param>
/// <param name="Budgets">
/// Lista do multiselecta nad tabelą. Jedzie RAZEM z listą, a nie osobnym żądaniem, bo ekran
/// i tak nie umie się narysować bez obu — dokładnie tak, jak robi to dashboard.
/// </param>
/// <param name="HasAnyTransactions">
/// Czy WYBRANE budżety mają jakiekolwiek transakcje, bez względu na filtr. Odróżnia drugi pusty
/// stan („budżet bez transakcji") od trzeciego („brak wyników filtra") — oba dają `Total == 0`,
/// ale wymagają innego komunikatu (import vs. wyczyść filtry).
/// </param>
/// <param name="StandingOrderName">
/// Nazwa zlecenia stałego z filtra (<see cref="TransactionFilterRequestDto.StandingOrderId"/>) — do etykiety filtra
/// „Zlecenie stałe: Czynsz ✕”. <c>null</c> bez filtra albo gdy zlecenia już nie ma.
/// </param>
public sealed record TransactionListResponseDto(
    IReadOnlyList<TransactionListItemResponseDto> Items,
    int Total,
    int Page,
    int PageSize,
    TransactionSummaryResponseDto Summary,
    IReadOnlyList<Guid> SelectedBudgetIds,
    IReadOnlyList<TransactionBudgetOptionResponseDto> Budgets,
    bool HasAnyTransactions,
    string? StandingOrderName = null);
