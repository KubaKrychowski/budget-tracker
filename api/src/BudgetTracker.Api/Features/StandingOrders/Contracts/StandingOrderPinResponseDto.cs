namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Ostatnio przypięta transakcja — karta „Ostatnio przypięte transakcje” z przyciskiem „Odepnij”.</summary>
/// <param name="Amount">Kwota wydatku, dodatnia.</param>
/// <param name="DifferentAmount">Czy kwota różni się od zwykłej kwoty zlecenia.</param>
public sealed record StandingOrderPinResponseDto(
    Guid TransactionId,
    Guid StandingOrderId,
    string StandingOrderName,
    DateOnly Date,
    decimal Amount,
    bool DifferentAmount);
