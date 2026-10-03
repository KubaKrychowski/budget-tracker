using BudgetTracker.Api.Features.Strategies.Consts;

namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Założenie nowej strategii — pustej albo z szablonu.</summary>
/// <param name="BudgetId">Budżet strategii; <c>null</c> = budżet domyślny.</param>
/// <param name="Name">Nazwa strategii, 1–100 znaków.</param>
/// <param name="Template">Od czego zacząć.</param>
public sealed record CreateStrategyRequestDto(Guid? BudgetId, string Name, StrategyTemplate Template);
