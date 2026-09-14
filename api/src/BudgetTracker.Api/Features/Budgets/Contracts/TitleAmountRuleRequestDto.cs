namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>Reguła transferu w zapisie — jeden wiersz sekcji „Reguły dopasowania” w ustawieniach powiązania.</summary>
/// <param name="TitlePattern">Fraza, którą musi zawierać opis transakcji — bez rozróżniania wielkości liter.</param>
/// <param name="AmountFrom">Dolna granica kwoty BEZWZGLĘDNEJ, bez znaku.</param>
/// <param name="AmountTo">Górna granica kwoty BEZWZGLĘDNEJ, bez znaku.</param>
public sealed record TitleAmountRuleRequestDto(string TitlePattern, decimal AmountFrom, decimal AmountTo);
