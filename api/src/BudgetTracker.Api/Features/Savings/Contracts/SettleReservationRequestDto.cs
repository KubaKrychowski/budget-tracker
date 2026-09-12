namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Rozliczenie rezerwacji wskazaną wypłatą.</summary>
/// <param name="TransactionId">
/// Publiczny identyfikator wypłaty z oszczędności, która zamyka rezerwację.
/// Wskazanie jest ZAWSZE ręczne — patrz <see cref="SettleCandidateResponseDto"/>.
/// </param>
public sealed record SettleReservationRequestDto(Guid TransactionId);
