namespace BudgetTracker.Api.Features.Dashboard.Contracts;

/// <summary>
/// Punkt wykresu obszarowego: BILANS budżetu w danym dniu.
/// </summary>
/// <remarks>
/// Nie ma tu linii limitu: zestawianie bilansu z sumą limitów per kategoria to porównywanie
/// dwóch różnych wielkości. Limity wracają z Etapem 1, jako osobny widok.
/// </remarks>
public record BudgetPointResponseDto(DateOnly Date, decimal Balance);
