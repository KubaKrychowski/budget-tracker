namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Ekran „Zlecenia stałe” dla jednego budżetu i jednego miesiąca — wszystko jednym żądaniem.</summary>
/// <param name="Month">Klucz oglądanego okresu — pierwszy dzień miesiąca, w którym okres rozliczeniowy się kończy.</param>
/// <param name="CurrentMonth">Klucz bieżącego okresu rozliczeniowego budżetu.</param>
/// <param name="PeriodStartDay">Dzień początku okresu rozliczeniowego budżetu (1–28).</param>
/// <param name="PeriodFrom">Pierwszy dzień oglądanego okresu; <c>null</c> tylko, gdy nie ma budżetu.</param>
/// <param name="PeriodTo">Ostatni dzień oglądanego okresu (włącznie); <c>null</c> tylko, gdy nie ma budżetu.</param>
/// <param name="MonthlyTotal">
/// Stałe zlecenia w przeliczeniu na miesiąc: miesięczne w całości, kwartalne ÷ 3, roczne ÷ 12. Bez tego polisa
/// roczna albo znikałaby z sumy, albo zawyżała jeden miesiąc dwunastokrotnie.
/// </param>
/// <param name="DueCount">Ile zleceń przypada na oglądany miesiąc.</param>
/// <param name="PaidCount">Ile z nich już zeszło (w zwykłej albo innej kwocie).</param>
/// <param name="WaitingAmount">Suma zwykłych kwot zleceń, które czekają — „czeka do zapłaty”.</param>
/// <param name="Recent">Ostatnio przypięte transakcje, od najnowszej.</param>
public sealed record StandingOrdersResponseDto(
    DateOnly Month,
    DateOnly CurrentMonth,
    IReadOnlyList<StandingOrderRowResponseDto> Orders,
    IReadOnlyList<StandingOrderPinResponseDto> Recent,
    decimal MonthlyTotal,
    int DueCount,
    int PaidCount,
    decimal WaitingAmount,
    int DifferentAmountCount,
    IReadOnlyList<Guid> SelectedBudgetIds,
    IReadOnlyList<StandingOrdersBudgetOptionResponseDto> Budgets,
    int PeriodStartDay = 1,
    DateOnly? PeriodFrom = null,
    DateOnly? PeriodTo = null);
