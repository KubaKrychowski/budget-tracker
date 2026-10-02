using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Limits.Models;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Limits.Services;

/// <summary>Kategorie, na które da się nałożyć limit wydatków.</summary>
/// <remarks>
/// <para>
/// Limit dotyczy wyłącznie kategorii wydatkowych (<see cref="CategoryType.Expense"/>) — limit wydatków na „Wynagrodzenie" nie znaczy nic.
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
        return await db.Categories
            .Where(c => c.Name != SavingsCategory.Name && c.Type == CategoryType.Expense)
            .OrderBy(c => c.Name)
            .Select(c => new LimitCategory(c.Id, c.BusinessId, c.Name))
            .ToListAsync(ct);
    }
}
