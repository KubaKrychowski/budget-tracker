using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categories.Services;

/// <summary>Gdzie kategoria jest używana — jedno źródło prawdy dla listy, blokady typu i blokady usunięcia.</summary>
/// <remarks>
/// Liczy to, co widzi zalogowane konto (filtr Owner i RLS), więc kategoria wspólna pokazuje użycie tylko przez to konto.
/// Zlecenia epizodyczne i wpłaty na rezerwacje nie mają osobnej kolumny na liście, ale też blokują usunięcie.
/// </remarks>
public sealed class CategoryUsage(AppDbContext db)
{
    /// <summary>Liczniki użycia każdej widocznej kategorii, po kluczu wewnętrznym.</summary>
    public async Task<IReadOnlyDictionary<int, (int Transactions, int Rules, int Limits)>> CountsAsync(CancellationToken ct)
    {
        var transactions = await db.Transactions.Where(t => t.CategoryId != null)
            .GroupBy(t => t.CategoryId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var rules = await db.CategoryRules
            .GroupBy(r => r.CategoryId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var limits = await db.BudgetItems
            .GroupBy(i => i.CategoryId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return transactions.Keys.Concat(rules.Keys).Concat(limits.Keys).Distinct().ToDictionary(
            id => id,
            id => (transactions.GetValueOrDefault(id), rules.GetValueOrDefault(id), limits.GetValueOrDefault(id)));
    }

    /// <summary>Czy kategoria jest używana przez transakcje, reguły, limity, zlecenia epizodyczne albo wpłaty na rezerwacje.</summary>
    public async Task<bool> InUseAsync(Category category, CancellationToken ct) =>
        await db.Transactions.AnyAsync(t => t.CategoryId == category.Id, ct)
        || await db.CategoryRules.AnyAsync(r => r.CategoryId == category.Id, ct)
        || await db.BudgetItems.AnyAsync(i => i.CategoryId == category.Id, ct)
        || await db.EpisodicOrders.AnyAsync(o => o.CategoryId == category.Id, ct)
        || await db.SavingsReservations.AnyAsync(
            r => r.Contributions.Any(c => c.CategoryBusinessId == category.BusinessId), ct);

    /// <summary>Widok kategorii z licznikami.</summary>
    public static CategoryResponseDto View(Category category, (int Transactions, int Rules, int Limits) counts) =>
        new(category.BusinessId, category.Name, category.Type, category.IsShared,
            counts.Transactions, counts.Rules, counts.Limits,
            InUse: counts.Transactions + counts.Rules + counts.Limits > 0);
}
