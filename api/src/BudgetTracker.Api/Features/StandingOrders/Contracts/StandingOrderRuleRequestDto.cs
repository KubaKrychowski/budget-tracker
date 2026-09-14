namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Reguła zlecenia w zapisie i podglądzie — jeden wiersz sekcji „Reguły dopasowania” w modalu.</summary>
/// <param name="TitlePattern">Fraza, którą musi zawierać opis transakcji — bez rozróżniania wielkości liter.</param>
/// <param name="AmountFrom">Dolna granica kwoty wydatku, bez znaku.</param>
/// <param name="AmountTo">Górna granica kwoty wydatku, bez znaku.</param>
public sealed record StandingOrderRuleRequestDto(string TitlePattern, decimal AmountFrom, decimal AmountTo);
