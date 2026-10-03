namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Stan na koniec jednego miesiąca symulacji.</summary>
/// <param name="Month">Klucz miesiąca (pierwszy dzień).</param>
/// <param name="Cash">Gotówka (oszczędności).</param>
/// <param name="Debt">Saldo długu.</param>
/// <param name="Interest">Odsetki naliczone w tym miesiącu.</param>
public sealed record StrategyMonthResponseDto(DateOnly Month, decimal Cash, decimal Debt, decimal Interest);
