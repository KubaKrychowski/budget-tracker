namespace BudgetTracker.Api.Features.Dashboard.Contracts;

/// <summary>Kafle nad wykresami dashboardu.</summary>
/// <param name="BudgetBalance">
/// Bilans NA KONIEC okresu, nie „ile zostało z limitów". Ostatni punkt <see cref="DashboardResponseDto.BudgetProgress"/>
/// jest tą samą liczbą, więc kafel i wykres nie mogą się rozjechać.
/// </param>
/// <param name="LargestExpenseAmount">Jako wartość dodatnia — tak, jak prezentuje ją UI.</param>
public record DashboardMetricsResponseDto(
    decimal TotalExpenses,
    decimal TotalIncome,
    decimal BudgetBalance,
    string? TopCategoryName,
    decimal TopCategoryAmount,
    string? LargestExpenseDescription,
    decimal LargestExpenseAmount,
    int ToReviewCount);
