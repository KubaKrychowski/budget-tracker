namespace BudgetTracker.Api.Features.EpisodicOrders.Contracts;

/// <summary>„Oznacz jako kupione…” — transakcja, którą zapłacono zaplanowany zakup.</summary>
public sealed record PurchaseEpisodicOrderRequestDto(Guid TransactionId);
