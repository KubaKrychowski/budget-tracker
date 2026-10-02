using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categories.Services;

/// <summary>Kategoria po publicznym identyfikatorze — wspólne dla zapisów.</summary>
public sealed class CategoryLookup(AppDbContext db)
{
    /// <summary>Kategoria, którą wolno zmienić; nieznana kończy się 404, wspólna (bazowa) 409.</summary>
    /// <remarks>
    /// ⚠️ Kategorie wspólne widzą wszyscy, ale RLS pozwala zmieniać tylko własne. Bez tej kontroli edycja wspólnej
    /// kategorii przeszłaby przez EF i skończyła się w bazie zmianą 0 wierszy, czyli błędem 500 zamiast czytelnej odpowiedzi.
    /// </remarks>
    public async Task<Category> FindOwnAsync(Guid id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.BusinessId == id, ct)
                       ?? throw new CategoryNotFoundException(id);
        return category.IsShared ? throw new CategorySharedReadOnlyException(id) : category;
    }
}
