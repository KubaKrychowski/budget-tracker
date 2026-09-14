using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Limits.Exceptions;

/// <summary>Limit o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class LimitNotFoundException(Guid businessId) : EntityNotFoundException("BudgetItem", businessId);
