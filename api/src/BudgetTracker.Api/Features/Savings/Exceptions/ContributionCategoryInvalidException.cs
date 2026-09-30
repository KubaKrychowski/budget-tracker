namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Wpłata ze zwykłego konta bez kategorii limitu albo z kategorią, na którą nie da się ustawić limitu.</summary>
public sealed class ContributionCategoryInvalidException : Exception;
