using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categories.Services;

/// <summary>Kategorie wspólne ukryte przez zalogowane konto — jedno źródło prawdy dla list wyboru i kategoryzacji.</summary>
/// <remarks>
/// Zbiór ładuje się RAZ na żądanie (usługa jest <c>Scoped</c>): kategoryzator woła go dla każdego wiersza importu, a zapytanie na wiersz
/// byłoby zapytaniem na darmo. ⚠️ Z tego powodu handlery, które SAME zmieniają ukrycie, nie czytają tego zbioru po zapisie.
/// </remarks>
public sealed class HiddenCategories(AppDbContext db)
{
    private IReadOnlySet<int>? _ids;

    /// <summary>Klucze kategorii ukrytych przez to konto (filtr Owner zawęża do zalogowanego).</summary>
    public async Task<IReadOnlySet<int>> IdsAsync(CancellationToken ct) =>
        _ids ??= (await db.HiddenCategories.Select(h => h.CategoryId).ToListAsync(ct)).ToHashSet();
}
