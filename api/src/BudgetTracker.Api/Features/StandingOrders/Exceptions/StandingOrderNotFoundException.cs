using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.StandingOrders.Exceptions;

/// <summary>Zlecenie stałe o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class StandingOrderNotFoundException(Guid businessId) : EntityNotFoundException("StandingOrder", businessId);
