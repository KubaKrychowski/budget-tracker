namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>Budżet jako pozycja selektora w kroku 1 importu.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> budżetu.</param>
public sealed record ImportBudgetOptionResponseDto(Guid Id, string Name, DateOnly Month);
