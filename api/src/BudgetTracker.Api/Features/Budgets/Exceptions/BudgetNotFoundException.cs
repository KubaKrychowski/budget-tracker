using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>Budżet o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class BudgetNotFoundException(Guid businessId)
    : EntityNotFoundException("Budget", businessId);
