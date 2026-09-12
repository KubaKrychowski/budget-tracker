namespace BudgetTracker.Api.Features.Categorization.Exceptions;

/// <summary>
/// Wzorzec nie kompiluje się jako wyrażenie regularne.
/// </summary>
/// <remarks>
/// ⚠️ Sprawdzane przy zapisie, nie przy użyciu. <c>RuleCategorizer</c> celowo połyka zły wzorzec
/// w czasie importu (żeby jedna zepsuta reguła nie wywaliła całego pliku), więc bez tej walidacji
/// błąd byłby całkowicie niewidoczny: reguła istnieje, wygląda poprawnie i nigdy nie działa.
/// </remarks>
public sealed class RulePatternInvalidException : Exception;
