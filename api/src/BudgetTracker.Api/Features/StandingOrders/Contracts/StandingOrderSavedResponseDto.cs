namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Zapisane zlecenie i liczba transakcji przypiętych po zapisie.</summary>
public sealed record StandingOrderSavedResponseDto(Guid Id, int LinkedCount);
