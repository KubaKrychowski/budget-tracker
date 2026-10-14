using System.Text.RegularExpressions;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.ProductCategories.Services;

/// <summary>Kategoryzacja produktów regułami po nazwie — pierwsza linia, przed modelem.</summary>
/// <remarks>
/// <para>
/// Reguły ładują się raz na instancję (scoped, jak <c>RuleCategorizer</c>): paragon ma kilkanaście pozycji, a odpytywanie bazy
/// per pozycja byłoby N+1. Pierwsza pasująca wygrywa; reguły nauczone od użytkownika mają niższy numer priorytetu niż bazowe.
/// </para>
/// <para>Zły wzorzec w bazie jest pomijany, nie wywala wgrania paragonu — reguła jest danymi, nie kodem.</para>
/// </remarks>
public sealed class ProductRuleCategorizer(AppDbContext db)
{
    private List<(Regex Pattern, Guid CategoryId)>? _rules;

    /// <summary><c>BusinessId</c> kategorii dla znormalizowanej nazwy albo <c>null</c>, gdy żadna reguła nie pasuje.</summary>
    public async Task<Guid?> CategorizeAsync(string normalizedName, CancellationToken ct)
    {
        if (normalizedName.Length == 0) return null;

        _rules ??= await LoadAsync(ct);
        foreach (var (pattern, categoryId) in _rules)
        {
            try
            {
                if (pattern.IsMatch(normalizedName)) return categoryId;
            }
            catch (RegexMatchTimeoutException)
            {
            }
        }

        return null;
    }

    private async Task<List<(Regex Pattern, Guid CategoryId)>> LoadAsync(CancellationToken ct)
    {
        var rows = await db.ProductRules
            .Join(db.ProductCategories, r => r.ProductCategoryId, c => c.Id,
                (r, c) => new { r.Priority, r.Id, r.Pattern, CategoryId = c.BusinessId })
            .OrderBy(x => x.Priority).ThenBy(x => x.Id)
            .ToListAsync(ct);

        var compiled = new List<(Regex Pattern, Guid CategoryId)>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                compiled.Add((RuleMatcher.Compile(row.Pattern)!, row.CategoryId));
            }
            catch (ArgumentException)
            {
            }
        }

        return compiled;
    }
}
