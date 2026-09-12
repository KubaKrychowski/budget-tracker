using BudgetTracker.Api.Features.Budgets.Consts;

namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>
/// Wiersz listy budżetów w ustawieniach.
/// </summary>
/// <param name="MonthlyLimit">
/// Suma limitów per kategoria (<c>BudgetItem</c>). Dziś zawsze wyliczana, nigdy edytowana —
/// czy to ma być osobne pole na budżecie, rozstrzyga issue #5.
/// </param>
/// <param name="Balance">
/// Bilans bieżący: <c>InitialBalance</c> plus suma transakcji budżetu. Ta sama formuła co na
/// dashboardzie — dwa ekrany nie mogą pokazywać dla tego samego budżetu dwóch różnych kwot.
/// </param>
/// <param name="TransactionCount">
/// Liczba żywych transakcji. Nie ozdoba: po resecie ma spaść do zera i to jest widoczny dowód,
/// że reset zadziałał.
/// </param>
public sealed record BudgetListItemResponseDto(
    Guid Id,
    string Name,
    DateOnly Month,
    string Currency,
    decimal InitialBalance,
    decimal Balance,
    decimal MonthlyLimit,
    DateTimeOffset CreatedAt,
    int TransactionCount,
    BudgetStatus Status,
    DateTimeOffset? DisabledAt,
    DateTimeOffset? DeletedAt);
