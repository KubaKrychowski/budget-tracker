namespace BudgetTracker.Api.Features.ProductCategories.Contracts;

/// <summary>Własna kategoria produktu przy tworzeniu.</summary>
/// <param name="Name">Nazwa; przycinana, niepusta, do 100 znaków, unikalna wśród własnych i wspólnych kategorii produktów.</param>
public sealed record ProductCategoryRequestDto(string? Name);
