namespace BudgetTracker.Api.Features.Categorization.Exceptions;

/// <summary>Reguła bez <c>Pattern</c> i bez <c>TransactionTypePattern</c> — patrz doc <c>CategoryRuleRequestDto</c>.</summary>
public sealed class RulePatternRequiredException : Exception;
