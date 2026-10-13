using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categories.Commands;

/// <summary>Przywraca ukrytą kategorię wspólną (zgłoszenie #30); idempotentne — kategoria nieukryta zostaje, jaka była.</summary>
public sealed class UnhideCategoryCommandHandler(AppDbContext db, CategoryUsage usage)
{
    public async Task<CategoryResponseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.BusinessId == id, ct)
            ?? throw new CategoryNotFoundException(id);

        var hidden = await db.HiddenCategories.Where(h => h.CategoryId == category.Id).ToListAsync(ct);
        if (hidden.Count > 0)
        {
            db.HiddenCategories.RemoveRange(hidden);
            await db.SaveChangesAsync(ct);
        }

        var counts = await usage.CountsAsync(ct);
        return CategoryUsage.View(category, counts.GetValueOrDefault(category.Id));
    }
}
