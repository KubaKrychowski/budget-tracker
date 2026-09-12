namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>Wszystko, czego stepper importu potrzebuje z góry — w jednym żądaniu.</summary>
/// <param name="Banks">Same klucze banków — nazwę tłumaczy front, tak samo jak enumy (CLAUDE.md §5).</param>
/// <param name="Budgets">Budżety do kroku 1, bez wyłączonych.</param>
/// <param name="Categories">Kategorie do korekty w kroku 3.</param>
public sealed record ImportSourcesResponseDto(
    IReadOnlyList<string> Banks,
    IReadOnlyList<ImportBudgetOptionResponseDto> Budgets,
    IReadOnlyList<ImportCategoryOptionResponseDto> Categories);
