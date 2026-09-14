namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Jedna wpłata na rezerwację.</summary>
public sealed record SavingsContributionResponseDto(Guid Id, DateOnly Date, decimal Amount);
