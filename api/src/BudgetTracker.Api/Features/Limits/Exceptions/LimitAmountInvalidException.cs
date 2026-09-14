namespace BudgetTracker.Api.Features.Limits.Exceptions;

/// <summary>Limit musi być dodatni — limit 0 zł to nie limit, tylko zakaz wydawania.</summary>
public sealed class LimitAmountInvalidException : Exception;
