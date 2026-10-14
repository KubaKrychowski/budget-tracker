using BudgetTracker.Api.Features.ProductCategories.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.ProductCategories.Queries;

/// <summary>Kategorie produktów widoczne dla konta: wspólne i własne, alfabetycznie.</summary>
public sealed class GetProductCategoryListQueryHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<ProductCategoryResponseDto>> HandleAsync(CancellationToken ct)
    {
        var categories = await db.ProductCategories.OrderBy(c => c.Name).ToListAsync(ct);
        return [.. categories.Select(c => new ProductCategoryResponseDto(c.BusinessId, c.Name, c.IsShared))];
    }
}
