using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.StandingOrders.Consts;

namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Wiersz tabeli zleceń stałych — zlecenie, jego reguły i stan w oglądanym miesiącu.</summary>
/// <param name="Rules">Reguły dopasowania w kolejności z modala.</param>
/// <param name="EndMonth">Pierwszy dzień ostatniego miesiąca zakończonego zlecenia; <c>null</c> = trwa.</param>
/// <param name="CategoryName">
/// Najczęstsza kategoria PRZYPIĘTYCH transakcji. Zlecenie nie ma własnej kategorii (tylko się przypina), więc to
/// jedyny uczciwy odczyt — i przy okazji pokazuje, gdy reguły kategoryzacji wkładają czynsz do „Inne”.
/// </param>
/// <param name="State"><see cref="StandingOrderMonthState.Ended"/> dla miesięcy po ostatnim — front odkłada je do „Zakończonych”.</param>
/// <param name="PaidOn">Data ostatniej przypiętej transakcji w miesiącu; <c>null</c>, gdy nic nie zeszło.</param>
/// <param name="PaidAmount">Suma przypiętych wydatków w miesiącu (dodatnia); <c>null</c>, gdy nic nie zeszło.</param>
/// <param name="UsualDay">Mediana dnia miesiąca z historii przypięć — „zwykle do 15.”; <c>null</c> bez historii.</param>
/// <param name="LinkedCount">Ile transakcji z całej historii jest przypiętych — licznik przy „Przejdź do powiązanych”.</param>
public sealed record StandingOrderRowResponseDto(
    Guid Id,
    string Name,
    decimal ExpectedAmount,
    StandingOrderRhythm Rhythm,
    int? DueMonth,
    IReadOnlyList<StandingOrderRuleResponseDto> Rules,
    DateOnly? EndMonth,
    string? CategoryName,
    StandingOrderMonthState State,
    DateOnly? PaidOn,
    decimal? PaidAmount,
    int? UsualDay,
    int LinkedCount);
