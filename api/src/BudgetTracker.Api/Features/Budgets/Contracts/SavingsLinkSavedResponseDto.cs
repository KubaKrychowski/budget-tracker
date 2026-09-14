namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>Zapisane powiązanie i liczba własnych transakcji budżetu przypiętych jako transfer po zapisie.</summary>
public sealed record SavingsLinkSavedResponseDto(Guid? LinkedSavingsBudgetId, int LinkedCount);
