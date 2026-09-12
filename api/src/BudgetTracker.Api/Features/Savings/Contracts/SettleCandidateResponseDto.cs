namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>
/// Kandydat do rozliczenia — „czy to było ubezpieczenie?".
/// </summary>
/// <remarks>
/// ⚠️ <b>Miesiąc terminu NIE jest filtrem, tylko kryterium kolejności.</b> Własny przykład
/// z makiety (170:1727) tego wymaga: proponuje wypłatę z <b>14.08.2026</b> dla rezerwacji
/// z terminem <b>maj 2026</b> — i słusznie, bo rachunki płaci się po terminie. Twardy filtr
/// miesiąca nigdy by tego kandydata nie pokazał.
/// </remarks>
/// <param name="Id">Publiczny <c>BusinessId</c> transakcji.</param>
public sealed record SettleCandidateResponseDto(Guid Id, DateOnly Date, decimal Amount, string Description);
