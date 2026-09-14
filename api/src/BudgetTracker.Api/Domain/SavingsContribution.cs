namespace BudgetTracker.Api.Domain;

/// <summary>Umowna wpłata na rezerwację — „odkładam 500 zł z oszczędności na rower”.</summary>
/// <remarks>
/// Pieniądze NIE zmieniają konta: wpłata mówi tylko, na co odłożona część oszczędności jest przeznaczona. Zapisuje się
/// razem z rezerwacją (kolumna <c>jsonb</c>), więc kasuje się i przywraca razem z nią. Identyfikator jest po to, żeby
/// dało się wycofać KONKRETNĄ wpłatę, a nie „ostatnie 500 zł”.
/// </remarks>
/// <param name="Id">Identyfikator wpłaty — adres „Wycofaj”.</param>
/// <param name="Date">Dzień wpłaty.</param>
/// <param name="Amount">Kwota, dodatnia.</param>
public sealed record SavingsContribution(Guid Id, DateOnly Date, decimal Amount);
