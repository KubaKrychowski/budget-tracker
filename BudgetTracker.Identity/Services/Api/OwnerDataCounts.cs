namespace BudgetTracker.Identity.Services.Api;

/// <summary>Ile wierszy danych właściciel ma w API budżetu — lustro kontraktu <c>OwnerDataCountsResponseDto</c>.</summary>
public sealed record OwnerDataCounts(
    int Budgets,
    int BudgetItems,
    int Transactions,
    int ImportBatches,
    int SavingsGoals,
    int SavingsReservations,
    int StandingOrders,
    int EpisodicOrders,
    int ModelVersions)
{
    public static OwnerDataCounts Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    public int Total => Budgets + BudgetItems + Transactions + ImportBatches
        + SavingsGoals + SavingsReservations + StandingOrders + EpisodicOrders + ModelVersions;
}
