namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Kategoria jako pozycja filtra i selecta w edycji.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> kategorii.</param>
public sealed record CategoryOptionResponseDto(Guid Id, string Name);
