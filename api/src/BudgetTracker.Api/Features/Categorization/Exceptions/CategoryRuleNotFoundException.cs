using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Categorization.Exceptions;

/// <summary>Reguła o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class CategoryRuleNotFoundException(Guid businessId)
    : EntityNotFoundException("CategoryRule", businessId);
