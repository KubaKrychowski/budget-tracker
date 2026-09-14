namespace BudgetTracker.Api.Features.EpisodicOrders.Contracts;

/// <summary>Budżet w przełączniku ekranu zleceń epizodycznych.</summary>
public sealed record EpisodicOrdersBudgetOptionResponseDto(Guid Id, string Name, DateOnly Month, bool Disabled);
