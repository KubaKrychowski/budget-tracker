namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Lista rezerwacji razem ze stanem konta oszczędnościowego i wolnymi środkami.</summary>
/// <param name="AccountBalance">
/// Stan konta oszczędnościowego. ⚠️ <b>Fallback do czasu #10</b>: suma wpłat minus suma wypłat
/// w kategorii „Oszczędności", czyli „ile netto przesunąłeś na oszczędności", a nie saldo konta.
/// Nie obejmuje salda otwarcia ani odsetek — ekran musi to powiedzieć wprost.
/// </param>
/// <param name="ReservedTotal">Suma kwot rezerwacji NIEROZLICZONYCH.</param>
/// <param name="CollectedTotal">Suma wpłat na rezerwacje nierozliczone.</param>
/// <param name="AvailableToContribute">
/// Ile jeszcze da się wpłacić na cele: stan konta minus wpłaty na rezerwacje nierozliczone. Umownie nie odłożysz
/// więcej, niż leży na koncie.
/// </param>
/// <param name="FreeFunds">
/// <c>AccountBalance − ReservedTotal</c>. Może wyjść ujemne i wtedy front pokazuje osobny stan
/// „rezerwacje przekraczają stan konta o X", a nie minus w kaflu — minus czyta się jak błąd.
///
/// <para>
/// ⚠️ <b>Rozliczona rezerwacja przestaje pomniejszać tę liczbę</b> (zgłoszenie #23, decyzja użytkownika). Wcześniej
/// koperta była roczna i odejmowała się także po rozliczeniu; teraz zapłacony zakup zszedł już ze stanu konta,
/// więc odejmowanie go drugi raz zaniżałoby wolne środki podwójnie. Wpłaty tej liczby nie zmieniają — zmienia ją
/// pełna kwota rezerwacji.
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
    decimal AvailableToContribute,
    decimal FreeFunds,
    DateOnly? CoveredBy,
    IReadOnlyList<Guid> SelectedBudgetIds,
    IReadOnlyList<SavingsBudgetOptionResponseDto> Budgets);
