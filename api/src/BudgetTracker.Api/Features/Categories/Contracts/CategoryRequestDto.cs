using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Categories.Contracts;

/// <summary>Własna kategoria przy tworzeniu i edycji.</summary>
/// <param name="Name">Nazwa; przycinana, niepusta, do 100 znaków, unikalna wśród własnych i wspólnych kategorii.</param>
/// <param name="Type">Wydatek albo wpływ; zmiana możliwa tylko, dopóki kategoria nie jest używana.</param>
public sealed record CategoryRequestDto(string? Name, CategoryType Type);
