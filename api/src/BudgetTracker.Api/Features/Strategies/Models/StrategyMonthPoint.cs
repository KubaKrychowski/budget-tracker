namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Stan na KONIEC miesiąca symulacji.</summary>
/// <param name="Month">Klucz miesiąca (pierwszy dzień).</param>
/// <param name="Cash">Gotówka (oszczędności).</param>
/// <param name="Debt">Saldo długu.</param>
/// <param name="Interest">Odsetki naliczone w tym miesiącu.</param>
public sealed record StrategyMonthPoint(DateOnly Month, decimal Cash, decimal Debt, decimal Interest);
