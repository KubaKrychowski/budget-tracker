using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>Transakcji nie ma albo nie jest przypięta jako transfer do żadnego budżetu oszczędnościowego.</summary>
public sealed class SavingsTransferPinNotFoundException(Guid transactionBusinessId)
    : EntityNotFoundException("Transaction", transactionBusinessId);
