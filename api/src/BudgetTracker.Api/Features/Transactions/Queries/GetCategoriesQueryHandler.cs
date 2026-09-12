using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Queries;

/// <summary>Słownik kategorii — do filtra i do selecta w edycji, alfabetycznie.</summary>
/// <remarks>Osobny odczyt, a nie część listy, bo front potrzebuje go, zanim jeszcze cokolwiek wczyta.</remarks>
public sealed class GetCategoriesQueryHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<CategoryOptionResponseDto>> HandleAsync(CancellationToken ct) =>
        await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryOptionResponseDto(c.BusinessId, c.Name))
            .ToListAsync(ct);
}
