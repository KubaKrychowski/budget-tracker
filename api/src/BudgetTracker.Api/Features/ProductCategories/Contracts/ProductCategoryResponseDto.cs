namespace BudgetTracker.Api.Features.ProductCategories.Contracts;

/// <summary>Kategoria produktu z paragonu.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> kategorii.</param>
/// <param name="Name">Nazwa widoczna w UI.</param>
/// <param name="IsShared">Kategoria wspólna (bazowa) — tylko do odczytu.</param>
public sealed record ProductCategoryResponseDto(Guid Id, string Name, bool IsShared);
