namespace BudgetTracker.Api.Domain;

/// <summary>Jedna reguła zlecenia stałego: opis transakcji ZAWIERA frazę, a kwota wydatku mieści się w zakresie.</summary>
/// <remarks>
/// Zakres zamiast jednej kwoty — czynsz po podwyżce dalej jest czynszem. Wartość bez tożsamości: reguły zapisują się
/// razem ze zleceniem (kolumna <c>jsonb</c>) i zmieniają tylko w całości.
/// </remarks>
/// <param name="TitlePattern">Fraza, którą musi ZAWIERAĆ opis transakcji (bez rozróżniania wielkości liter). To dane, nie wzorzec LIKE.</param>
/// <param name="AmountFrom">Dolna granica kwoty wydatku (wartość bezwzględna, włącznie).</param>
/// <param name="AmountTo">Górna granica kwoty wydatku (wartość bezwzględna, włącznie).</param>
public sealed record StandingOrderRule(string TitlePattern, decimal AmountFrom, decimal AmountTo);
