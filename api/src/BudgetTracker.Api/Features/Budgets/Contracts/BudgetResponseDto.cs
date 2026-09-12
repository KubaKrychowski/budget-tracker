namespace BudgetTracker.Api.Features.Budgets.Contracts;

public sealed record BudgetResponseDto(Guid Id, string Name, DateOnly Month, string Currency, decimal InitialBalance);