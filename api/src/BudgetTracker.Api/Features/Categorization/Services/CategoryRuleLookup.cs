using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>Reguła i kategoria po publicznych identyfikatorach — wspólne dla zapisów reguł.</summary>
public sealed class CategoryRuleLookup(AppDbContext db)
{
    /// <summary>Reguła o <paramref name="id"/>; nieznana kończy się 404.</summary>
    public async Task<CategoryRule> FindAsync(Guid id, CancellationToken ct) =>
        await db.Set<CategoryRule>().FirstOrDefaultAsync(r => r.BusinessId == id, ct)
        ?? throw new CategoryRuleNotFoundException(id);

    /// <summary>Kategoria, na którą wskazuje reguła; nieznana kończy się 404.</summary>
    public async Task<Category> CategoryAsync(Guid id, CancellationToken ct) =>
        await db.Categories.FirstOrDefaultAsync(c => c.BusinessId == id, ct)
        ?? throw new CategoryNotFoundException(id);
}
