using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Receipts.Exceptions;

/// <summary>Paragon o wskazanym <c>BusinessId</c> nie istnieje.</summary>
public sealed class ReceiptNotFoundException(Guid businessId) : EntityNotFoundException("Receipt", businessId);
