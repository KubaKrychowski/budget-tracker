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
    /// <summary>
    /// Pomija TYLKO filtr „SoftDelete" — budżet innego użytkownika ma dalej rzucać 404, tak jak
    /// nieznany identyfikator, żeby nie zdradzać, czy w ogóle istnieje (IDOR).
    /// </summary>
    /// <exception cref="BudgetNotFoundException">Nieznany identyfikator — nigdy cichy fallback na inny budżet.</exception>
    public async Task<Budget> FindIncludingDeletedAsync(Guid businessId, CancellationToken ct) =>
        await db.Budgets.IgnoreQueryFilters(["SoftDelete"])
            .FirstOrDefaultAsync(b => b.BusinessId == businessId, ct)
        ?? throw new BudgetNotFoundException(businessId);
}
