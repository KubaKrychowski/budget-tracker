namespace BudgetTracker.Api.Domain;

/// <summary>Limit wydatków budżetu na jedną kategorię (Etap 1).</summary>
public class BudgetItem(Guid budgetBusinessId, int categoryId, decimal limit) : Entity
{
    /// <summary>
    /// Budżet, do którego należy limit — publiczny identyfikator, tak samo jak
    /// <see cref="Transaction.BudgetBusinessId"/>. Świadomie bez relacji EF: zwykła kolumna,
    /// łączona z <see cref="Budget.BusinessId"/> w zapytaniach.
    /// </summary>
    public Guid BudgetBusinessId { get; protected set; } = budgetBusinessId;

    public int CategoryId { get; protected set; } = categoryId;

    /// <summary>Limit wydatków na kategorię, jako wartość dodatnia.</summary>
    public decimal Limit { get; protected set; } = limit;
}
