using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Transactions.Exceptions;

/// <summary>Transakcja o wskazanym <c>BusinessId</c> nie istnieje (np. edycja usuniętego wiersza).</summary>
public sealed class TransactionNotFoundException(Guid businessId)
    : EntityNotFoundException("Transaction", businessId);
