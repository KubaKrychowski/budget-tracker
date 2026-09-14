namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Wpłata większa niż to, co zostało na koncie oszczędnościowym po wpłatach na inne cele.</summary>
public sealed class ContributionExceedsBalanceException : Exception;
