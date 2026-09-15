namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>Reguła transferu w odpowiedzi — wartości startowe ekranu „Reguły powiązania”.</summary>
/// <param name="AmountFrom">Dolna granica kwoty BEZWZGLĘDNEJ, bez znaku.</param>
/// <param name="AmountTo">Górna granica kwoty BEZWZGLĘDNEJ, bez znaku.</param>
public sealed record TitleAmountRuleResponseDto(string TitlePattern, decimal AmountFrom, decimal AmountTo);
