namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Zlecenie stałe do wyboru w kafelku „Zakończ zlecenie stałe”.</summary>
public sealed record StrategyStandingOrderOptionResponseDto(Guid Id, string Name, decimal ExpectedAmount);
