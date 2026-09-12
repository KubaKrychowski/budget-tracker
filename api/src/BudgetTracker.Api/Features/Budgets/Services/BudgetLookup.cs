using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>
/// Wyszukanie budżetu po kluczu publicznym — także usuniętego, bo ekran ustawień operuje na obu
/// (przywracanie).
/// </summary>
public sealed class BudgetLookup(AppDbContext db)
{
    /// <exception cref="BudgetNotFoundException">Nieznany identyfikator — nigdy cichy fallback na inny budżet.</exception>
    public async Task<Budget> FindIncludingDeletedAsync(Guid businessId, CancellationToken ct) =>
        await db.Budgets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.BusinessId == businessId, ct)
        ?? throw new BudgetNotFoundException(businessId);
}
