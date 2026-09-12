using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Queries;

/// <summary>Reguły kategoryzacji w kolejności, w jakiej NAPRAWDĘ są sprawdzane.</summary>
/// <remarks>
/// Kolejność jak w silniku (priorytet rosnąco, potem <c>Id</c>). Posortowana inaczej, np. alfabetycznie,
/// lista ukrywałaby jedyną rzecz, która przy nachodzących wzorcach rozstrzyga wynik.
/// </remarks>
public sealed class GetCategoryRulesQueryHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<CategoryRuleResponseDto>> HandleAsync(CancellationToken ct)
    {
        var rows = await db.Set<CategoryRule>()
            .Join(db.Categories, r => r.CategoryId, c => c.Id, (r, c) => new { Rule = r, Category = c })
            .OrderBy(x => x.Rule.Priority)
            .ThenBy(x => x.Rule.Id)
            .ToListAsync(ct);

        return [.. rows.Select(x => CategoryRuleViews.Of(x.Rule, x.Category))];
    }
}
