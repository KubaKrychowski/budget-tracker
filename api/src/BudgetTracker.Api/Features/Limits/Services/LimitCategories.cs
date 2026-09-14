using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Limits.Models;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Limits.Services;

/// <summary>Kategorie, na które da się nałożyć limit wydatków.</summary>
/// <remarks>
/// <para>
/// Kategoria nie ma flagi „przychodowa", więc rozstrzygają reguły: kategoria, której WSZYSTKIE reguły
/// dotyczą wyłącznie wpływów (<see cref="RuleDirection.Income"/>), jest przychodowa — limit wydatków na
/// „Wynagrodzenie" nie znaczy nic. Kategoria bez reguł zostaje dozwolona: nie ma dowodu, że nie jest wydatkiem.
/// </para>
/// <para>
/// ⚠️ „Oszczędności" są wykluczone, choć przelew na oszczędności jest w bazie kwotą ujemną. To nie wydatek,
/// tylko przesunięcie pieniędzy (fallback do czasu #10, patrz <see cref="SavingsCategory"/>) — policzone
/// jako wydatek zawyżałoby „wydane poza limitami" o każdą odłożoną złotówkę. Nazwa pochodzi z
/// <see cref="SavingsCategory.Name"/>, a nie z kopii literału: dwie kopie rozjechałyby się po cichu.
/// </para>
/// </remarks>
public sealed class LimitCategories(AppDbContext db)
{
    /// <summary>Dozwolone kategorie: klucz w bazie, publiczny identyfikator i nazwa, alfabetycznie.</summary>
    public async Task<IReadOnlyList<LimitCategory>> AllowedAsync(CancellationToken ct)
    {
        var incomeOnly = await db.CategoryRules
            .GroupBy(r => r.CategoryId)
            .Where(g => g.All(r => r.Direction == RuleDirection.Income))
            .Select(g => g.Key)
            .ToListAsync(ct);

        return await db.Categories
            .Where(c => c.Name != SavingsCategory.Name && !incomeOnly.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new LimitCategory(c.Id, c.BusinessId, c.Name))
            .ToListAsync(ct);
    }
}
