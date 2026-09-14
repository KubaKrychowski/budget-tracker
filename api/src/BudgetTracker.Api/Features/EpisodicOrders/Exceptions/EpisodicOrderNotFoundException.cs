using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.EpisodicOrders.Exceptions;

/// <summary>Zlecenie epizodyczne o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class EpisodicOrderNotFoundException(Guid businessId) : EntityNotFoundException("EpisodicOrder", businessId);
