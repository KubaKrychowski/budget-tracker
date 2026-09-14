namespace BudgetTracker.Api.Features.Limits.Contracts;

/// <summary>Zapisany limit — identyfikator i miesiąc, od którego FAKTYCZNIE obowiązuje.</summary>
public sealed record LimitSavedResponseDto(Guid Id, decimal Limit, int WarningThreshold, DateOnly ValidFrom);
