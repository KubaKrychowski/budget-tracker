using BudgetTracker.Api.Features.Savings.Consts;

namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Rezerwacja — nazwana koperta na nadchodzący wydatek.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> rezerwacji.</param>
/// <param name="Collected">
/// Suma wpłat na tę rezerwację. Rozliczona ma tu pełną kwotę: te pieniądze już wyszły.
/// </param>
/// <param name="Contributions">Wpłaty od najnowszej — do listy z „Wycofaj” w dialogu wpłaty.</param>
/// <param name="DueMonth">Pierwszy dzień miesiąca terminu; <c>null</c> = „przy okazji”.</param>
/// <param name="SettledOn">Data wskazanej wypłaty; <c>null</c>, gdy nierozliczona.</param>
public sealed record SavingsReservationResponseDto(
    Guid Id,
    string Name,
    decimal Amount,
    DateOnly? DueMonth,
    decimal Collected,
    ReservationStatus Status,
    DateOnly? SettledOn,
    Guid? SettledTransactionId,
    IReadOnlyList<SavingsContributionResponseDto> Contributions);
