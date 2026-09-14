namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Ekran „Zlecenia stałe” dla jednego budżetu i jednego miesiąca — wszystko jednym żądaniem.</summary>
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
    IReadOnlyList<StandingOrdersBudgetOptionResponseDto> Budgets);
