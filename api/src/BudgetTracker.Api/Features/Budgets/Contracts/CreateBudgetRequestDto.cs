namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>
/// Treść kroku 1 kreatora budżetu („Dane podstawowe" z makiety).
///
/// Makieta pyta WYŁĄCZNIE o nazwę i walutę — miesiąca nie pyta, mimo że budżet jest
/// miesięczny. Miesiąc ustala serwer (bieżący).
/// </summary>
public sealed record CreateBudgetRequestDto(string Name, string Currency, decimal InitialBalance);
