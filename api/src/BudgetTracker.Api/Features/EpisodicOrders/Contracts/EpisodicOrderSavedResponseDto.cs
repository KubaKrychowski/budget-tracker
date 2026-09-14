namespace BudgetTracker.Api.Features.EpisodicOrders.Contracts;

/// <summary>Odpowiedź po zapisie — front i tak przeładowuje ekran, więc wystarczy identyfikator.</summary>
public sealed record EpisodicOrderSavedResponseDto(Guid Id);
