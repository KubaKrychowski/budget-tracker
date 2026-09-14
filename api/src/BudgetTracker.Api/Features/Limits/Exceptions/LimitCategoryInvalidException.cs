namespace BudgetTracker.Api.Features.Limits.Exceptions;

/// <summary>
/// Kategorii nie ma albo nie da się na nią nałożyć limitu (przychodowa, oszczędności).
/// </summary>
/// <remarks>
/// 400, nie 404: identyfikator kategorii przychodzi w CIELE żądania — 404 mówiłby „nie ma endpointu".
/// </remarks>
public sealed class LimitCategoryInvalidException : Exception;
