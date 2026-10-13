using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Queries;

/// <summary>Słownik kategorii — do filtra i do selecta w edycji, alfabetycznie.</summary>
/// <remarks>
/// <para>Osobny odczyt, a nie część listy, bo front potrzebuje go, zanim jeszcze cokolwiek wczyta.</para>
/// <para>
/// ⚠️ Kategorie ukryte przez konto (#30) są domyślnie POMIJANE — to słownik do WYBORU. <c>includeHidden</c> zwraca je z flagą
/// <c>Hidden</c> dla ekranu, który pokazuje już istniejące przypisania (transakcja w ukrytej kategorii nadal ma ją nazwać).
/// </para>
/// </remarks>
public sealed class GetCategoriesQueryHandler(AppDbContext db, HiddenCategories hiddenCategories)
{
    public async Task<IReadOnlyList<CategoryOptionResponseDto>> HandleAsync(bool includeHidden, CancellationToken ct)
    {
        var hidden = await hiddenCategories.IdsAsync(ct);
        var categories = await db.Categories.OrderBy(c => c.Name).Select(c => new { c.Id, c.BusinessId, c.Name, c.Type }).ToListAsync(ct);

        return [.. categories
            .Where(c => includeHidden || !hidden.Contains(c.Id))
            .Select(c => new CategoryOptionResponseDto(c.BusinessId, c.Name, c.Type, hidden.Contains(c.Id)))];
    }
}
