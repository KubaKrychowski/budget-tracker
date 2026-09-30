using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Umowna wpłata na rezerwację — „odkładam 500 zł z oszczędności na rower”.</summary>
/// <remarks>
/// Pieniądze NIE zmieniają konta: wpłata mówi tylko, na co odłożona część pieniędzy jest przeznaczona. Zapisuje się
/// razem z rezerwacją (kolumna <c>jsonb</c>), więc kasuje się i przywraca razem z nią. Identyfikator jest po to, żeby
/// dało się wycofać KONKRETNĄ wpłatę, a nie „ostatnie 500 zł”.
/// <para>
/// ⚠️ Wpłata ze zwykłego konta NIE jest transakcją — bank i tak wyśle prawdziwe wyciągi, więc druga transakcja
/// zdublowałaby ruch. Do limitu wlicza ją <c>LimitSpending</c> z samej listy wpłat, w okresie jej daty.
/// Kategoria to publiczny identyfikator, bez relacji EF — jak <see cref="SavingsReservation.BudgetBusinessId"/>.
/// </para>
/// </remarks>
/// <param name="Id">Identyfikator wpłaty — adres „Wycofaj”.</param>
/// <param name="Date">Dzień wpłaty.</param>
/// <param name="Amount">Kwota, dodatnia.</param>
/// <param name="Source">Konto, z którego odkładasz (<see cref="ContributionSource"/>).</param>
/// <param name="CategoryBusinessId">
/// Kategoria, do której limitu wlicza się wpłata ze zwykłego konta; <c>null</c> dla wpłaty z oszczędności.
/// </param>
public sealed record SavingsContribution(
    Guid Id, DateOnly Date, decimal Amount, ContributionSource Source, Guid? CategoryBusinessId);
