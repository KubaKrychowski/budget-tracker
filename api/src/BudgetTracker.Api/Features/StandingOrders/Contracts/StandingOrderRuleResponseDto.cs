namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Reguła zlecenia w wierszu tabeli — opis pod nazwą i wartości startowe modala „Zmień”.</summary>
/// <param name="AmountFrom">Dolna granica kwoty wydatku, bez znaku.</param>
/// <param name="AmountTo">Górna granica kwoty wydatku, bez znaku.</param>
public sealed record StandingOrderRuleResponseDto(string TitlePattern, decimal AmountFrom, decimal AmountTo);
