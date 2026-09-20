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

    /// <summary>Reguła o <paramref name="id"/>, którą wolno zmienić; nieznana kończy się 404, wspólna (bazowa) 409.</summary>
    /// <remarks>
    /// ⚠️ Reguły wspólne widzą wszyscy, ale RLS pozwala zmieniać tylko własne. Bez tej kontroli edycja wspólnej reguły
    /// przeszłaby przez EF i skończyła się w bazie zmianą 0 wierszy, czyli błędem 500 zamiast czytelnej odpowiedzi.
    /// </remarks>
    public async Task<CategoryRule> FindOwnAsync(Guid id, CancellationToken ct)
    {
        var rule = await FindAsync(id, ct);
        return rule.IsShared ? throw new CategoryRuleSharedReadOnlyException(id) : rule;
    }

    /// <summary>Kategoria, na którą wskazuje reguła; nieznana kończy się 404.</summary>
    public async Task<Category> CategoryAsync(Guid id, CancellationToken ct) =>
        await db.Categories.FirstOrDefaultAsync(c => c.BusinessId == id, ct)
        ?? throw new CategoryNotFoundException(id);
}
