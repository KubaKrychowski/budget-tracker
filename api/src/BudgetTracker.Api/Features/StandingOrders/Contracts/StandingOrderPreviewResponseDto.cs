namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Wynik podglądu reguły: liczba pasujących wydatków i ostatni z nich.</summary>
/// <param name="TakenByOtherOrders">
/// Ile z pasujących jest już przypiętych do INNEGO zlecenia — te nie przejdą do tego, bo transakcja należy do
/// jednego zlecenia naraz.
/// </param>
public sealed record StandingOrderPreviewResponseDto(int MatchCount, int TakenByOtherOrders, DateOnly? LastDate, decimal? LastAmount);
