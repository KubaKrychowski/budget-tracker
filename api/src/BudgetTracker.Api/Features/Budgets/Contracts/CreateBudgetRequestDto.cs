namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>
/// Treść kroku 1 kreatora budżetu („Dane podstawowe" z makiety).
///
/// Makieta pyta WYŁĄCZNIE o nazwę i walutę — miesiąca nie pyta, mimo że budżet jest
/// miesięczny. Miesiąc ustala serwer (bieżący).
/// </summary>
/// <param name="LinkedSavingsBudgetName">
/// Niepusta = dopisek „Dodaj połączony budżet oszczędnościowy": tworzy DRUGI budżet o tej nazwie
/// i powiązuje go z głównym w jednej operacji. <c>null</c>/pusta = brak powiązania (domyślnie).
/// </param>
public sealed record CreateBudgetRequestDto(
    string Name, string Currency, decimal InitialBalance, string? LinkedSavingsBudgetName = null);
