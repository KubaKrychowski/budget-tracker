using BudgetTracker.Api.Features.Savings.Consts;

namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Rezerwacja — nazwana koperta na nadchodzący wydatek.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> rezerwacji.</param>
/// <param name="Collected">
/// Ile z tej rezerwacji jest naprawdę pokryte pieniędzmi na koncie — przydział z kolejki,
/// nie procent od oka. Rezerwacja rozliczona ma tu pełną kwotę: te pieniądze już wyszły.
/// </param>
/// <param name="SettledOn">Data wskazanej wypłaty; <c>null</c>, gdy nierozliczona.</param>
public sealed record SavingsReservationResponseDto(
    Guid Id,
    string Name,
    decimal Amount,
    DateOnly DueMonth,
    decimal Collected,
    ReservationStatus Status,
    DateOnly? SettledOn,
    Guid? SettledTransactionId);
