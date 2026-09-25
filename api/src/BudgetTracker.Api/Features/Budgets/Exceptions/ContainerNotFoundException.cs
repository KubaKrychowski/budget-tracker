using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>Budżet o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class ContainerNotFoundException(Guid businessId)
    : EntityNotFoundException("Container", businessId);
