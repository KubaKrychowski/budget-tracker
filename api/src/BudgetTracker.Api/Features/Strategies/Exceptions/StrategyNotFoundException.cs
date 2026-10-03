using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Strategies.Exceptions;

/// <summary>Strategia o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class StrategyNotFoundException(Guid businessId) : EntityNotFoundException("Strategy", businessId);
