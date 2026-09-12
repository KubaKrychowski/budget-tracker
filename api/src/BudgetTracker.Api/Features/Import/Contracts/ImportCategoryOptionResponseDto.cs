namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>Kategoria jako pozycja selecta korekty w kroku 3 importu.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> kategorii.</param>
public sealed record ImportCategoryOptionResponseDto(Guid Id, string Name);
