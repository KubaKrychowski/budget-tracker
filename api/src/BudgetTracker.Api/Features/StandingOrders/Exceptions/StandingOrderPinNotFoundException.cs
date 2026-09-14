using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.StandingOrders.Exceptions;

/// <summary>Transakcji nie ma albo nie jest przypięta do żadnego zlecenia — nie ma czego odpinać.</summary>
public sealed class StandingOrderPinNotFoundException(Guid transactionBusinessId)
    : EntityNotFoundException("Transaction", transactionBusinessId);
