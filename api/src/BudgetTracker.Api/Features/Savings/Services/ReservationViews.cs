using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Savings.Consts;
using BudgetTracker.Api.Features.Savings.Contracts;

namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>Status i widok rezerwacji — wspólne dla listy i odpowiedzi po zapisie.</summary>
public static class ReservationViews
{
    /// <summary>Trzy statusy z makiety (171:1690) — rozliczenie jest nadrzędne nad terminem.</summary>
    public static ReservationStatus StatusOf(SavingsReservation r, DateOnly currentMonth) =>
        r.SettledAt is not null ? ReservationStatus.Settled
        : r.DueMonth is { } due && due < currentMonth ? ReservationStatus.Overdue
        : ReservationStatus.Collecting;

    /// <summary>
    /// Widok POJEDYNCZEJ rezerwacji, zwracany po zapisie.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>Collected</c> jest tu zerowe dla nierozliczonej i to jest świadome: przydział
    /// zależy od CAŁEJ kolejki, więc policzony dla jednego wiersza byłby zgadywanką. Front
    /// po zapisie i tak przeładowuje listę — tam liczba jest prawdziwa.
    /// </remarks>
    public static SavingsReservationResponseDto Single(SavingsReservation r, DateOnly currentMonth) =>
        new(r.BusinessId, r.Name, r.Amount, r.DueMonth,
            r.SettledAt is not null ? r.Amount : 0m,
            StatusOf(r, currentMonth),
            r.SettledAt is { } settled ? DateOnly.FromDateTime(settled.UtcDateTime.Date) : null,
            r.SettledTransactionBusinessId);
}
