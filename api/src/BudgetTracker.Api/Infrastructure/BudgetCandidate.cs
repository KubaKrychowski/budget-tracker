namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Budżet w kolejności rozstrzygania domyślnego: od najnowszego miesiąca.
/// Tyle, ile reguła wyboru naprawdę potrzebuje — nie cała encja.
/// </summary>
public readonly record struct BudgetCandidate(Guid BusinessId, DateOnly Month);
