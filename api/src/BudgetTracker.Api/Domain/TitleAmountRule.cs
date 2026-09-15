namespace BudgetTracker.Api.Domain;

/// <summary>
/// Reguła dopasowania: tytuł ZAWIERA frazę (jak <see cref="StandingOrderRule"/>) i kwota BEZWZGLĘDNA
/// mieści się w przedziale [<see cref="AmountFrom"/>, <see cref="AmountTo"/>], oba końce włącznie.
/// </summary>
/// <remarks>
/// W odróżnieniu od <see cref="StandingOrderRule"/> nie zakłada znaku kwoty — transfer do budżetu
/// oszczędnościowego jest wydatkiem (ujemny), a transfer z powrotem przychodem (dodatni), więc
/// dopasowanie musi łapać oba.
/// </remarks>
public sealed record TitleAmountRule(string TitlePattern, decimal AmountFrom, decimal AmountTo);
