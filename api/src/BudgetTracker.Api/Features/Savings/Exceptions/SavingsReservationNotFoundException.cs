using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Rezerwacja o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class SavingsReservationNotFoundException(Guid businessId)
    : EntityNotFoundException("SavingsReservation", businessId);
