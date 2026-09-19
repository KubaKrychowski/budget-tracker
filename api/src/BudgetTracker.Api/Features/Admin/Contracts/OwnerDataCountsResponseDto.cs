namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <summary>
/// Ile wierszy danych ma właściciel w każdej tabeli. Liczone WSZYSTKIE wiersze, także skasowane logicznie —
/// to dokładnie to, co zniknie przy trwałym usunięciu danych.
/// </summary>
public sealed record OwnerDataCountsResponseDto(
    int Budgets,
    int BudgetItems,
    int Transactions,
    int ImportBatches,
    int SavingsGoals,
    int SavingsReservations,
    int StandingOrders,
    int EpisodicOrders)
{
    public static OwnerDataCountsResponseDto Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    public int Total => Budgets + BudgetItems + Transactions + ImportBatches
        + SavingsGoals + SavingsReservations + StandingOrders + EpisodicOrders;
}
