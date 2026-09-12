using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Categorization.Exceptions;

/// <summary>Kategoria o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class CategoryNotFoundException(Guid businessId)
    : EntityNotFoundException("Category", businessId);
