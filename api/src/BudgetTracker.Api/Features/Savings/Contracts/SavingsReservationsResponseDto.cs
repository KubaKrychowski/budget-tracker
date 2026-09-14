namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Lista rezerwacji razem ze stanem konta oszczędnościowego i wolnymi środkami.</summary>
/// <param name="AccountBalance">
/// Stan konta oszczędnościowego. ⚠️ <b>Fallback do czasu #10</b>: suma wpłat minus suma wypłat
/// w kategorii „Oszczędności", czyli „ile netto przesunąłeś na oszczędności", a nie saldo konta.
/// Nie obejmuje salda otwarcia ani odsetek — ekran musi to powiedzieć wprost.
/// </param>
/// <param name="ReservedTotal">
/// Suma kwot <b>WSZYSTKICH</b> rezerwacji, także rozliczonych.
/// </param>
/// <param name="FreeFunds">
/// <c>AccountBalance − ReservedTotal</c>. Może wyjść ujemne i wtedy front pokazuje osobny stan
/// „rezerwacje przekraczają stan konta o X", a nie minus w kaflu — minus czyta się jak błąd.
///
/// <para>
/// ⚠️ <b>Rozliczenie tej liczby NIE zmienia</b> i to jest decyzja, nie przeoczenie (issue #11,
/// makieta 147:96: „9 400 zł − 5 000 zł rezerwacji" przy rozliczonych 1 800 zł). Koperta jest
/// ROCZNA — plan zobowiązań na cały rok, nie bieżący stan kopert. Myli się przy tym w bezpieczną
/// stronę: liczba nazwana „wolne środki" zachęca do wydania, więc jej zaniżenie kosztuje
/// najwyżej nadmierną ostrożność.
/// </para>
/// </param>
/// <param name="CoveredBy">
/// Miesiąc, w którym przy obecnym celu miesięcznym uzbiera się reszta rezerwacji
/// („resztę uzbierasz do maja"). <c>null</c>, gdy wszystko już pokryte albo gdy nie ma celu —
/// bez celu nie ma z czego liczyć tempa i zdanie byłoby zgadywanką.
/// </param>
/// <param name="Budgets">Budżety do przełącznika nad listą — patrz <see cref="SavingsBudgetOptionResponseDto"/>.</param>
public sealed record SavingsReservationsResponseDto(
    IReadOnlyList<SavingsReservationResponseDto> Reservations,
    decimal AccountBalance,
    decimal ReservedTotal,
    decimal SettledTotal,
    decimal CollectedTotal,
    decimal FreeFunds,
    DateOnly? CoveredBy,
    IReadOnlyList<Guid> SelectedBudgetIds,
    IReadOnlyList<SavingsBudgetOptionResponseDto> Budgets);
