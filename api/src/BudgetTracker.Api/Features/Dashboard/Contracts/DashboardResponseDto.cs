namespace BudgetTracker.Api.Features.Dashboard.Contracts;

/// <summary>Kontrakt jednego round-tripu dla całego ekranu dashboardu.</summary>
/// <param name="SelectedBudgetId">
/// Budżet, na którym faktycznie policzono liczby — wybrany przez usera albo domyślny.
/// UI mirroruje to pole zamiast zgadywać regułę wyboru po swojej stronie.
/// </param>
/// <param name="HasAnyTransactions">
/// Odróżnia „pusty okres" od „pusty budżet" — policzone dla budżetu z <paramref name="SelectedBudgetId"/>,
/// bez filtra dat. Empty state z CTA importu ma sens tylko w drugim przypadku: w pierwszym user
/// po prostu wybrał zakres bez danych, a budżet realnie ma transakcje gdzie indziej w historii.
/// Zasięg to CELOWO ten jeden budżet, nie cała baza — inny budżet z danymi nie może gasić CTA
/// tu, gdzie wybrany budżet jest faktycznie pusty.
/// </param>
public record DashboardResponseDto(
    DashboardMetricsResponseDto Metrics,
    IReadOnlyList<CategorySpendResponseDto> ByCategory,
    IReadOnlyList<BudgetPointResponseDto> BudgetProgress,
    IReadOnlyList<RecentTransactionResponseDto> RecentTransactions,
    IReadOnlyList<BudgetOptionResponseDto> Budgets,
    Guid? SelectedBudgetId,
    bool HasAnyTransactions);
