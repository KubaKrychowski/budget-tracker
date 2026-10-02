using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categories.Queries;

/// <summary>Lista zarządzania kategoriami: własne i wspólne z licznikami użycia, alfabetycznie.</summary>
/// <remarks>Osobna od słownika <c>GET /api/categories</c>, który jest lekki i potrzebny wszędzie, zanim cokolwiek się wczyta.</remarks>
public sealed class GetCategoryListQueryHandler(AppDbContext db, CategoryUsage usage)
{
    public async Task<IReadOnlyList<CategoryResponseDto>> HandleAsync(CancellationToken ct)
    {
        var counts = await usage.CountsAsync(ct);
        var categories = await db.Categories.OrderBy(c => c.Name).ToListAsync(ct);

        return [.. categories.Select(c => CategoryUsage.View(c, counts.GetValueOrDefault(c.Id)))];
    }
}
