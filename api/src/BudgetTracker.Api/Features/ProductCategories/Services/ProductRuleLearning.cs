using System.Text.RegularExpressions;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.ProductCategories.Services;

/// <summary>Zamienia poprawkę użytkownika w regułę: ta sama nazwa produktu dostanie kategorię sama na kolejnych paragonach.</summary>
/// <remarks>
/// <para>
/// Reguła to pełne dopasowanie znormalizowanej nazwy (<c>^nazwa$</c>), jedna na nazwę u właściciela. Poprawka tej samej nazwy
/// przestawia istniejącą regułę zamiast dokładać drugą, a zdjęcie kategorii (<c>null</c>) ją kasuje.
/// </para>
/// <para>⚠️ Nazwy krótsze niż <see cref="MinNameLength"/> znaków po normalizacji nie uczą reguły — „x” albo sama gramatura nie jest nazwą produktu.</para>
/// </remarks>
public sealed class ProductRuleLearning(AppDbContext db, ICurrentUserAccessor currentUser)
{
    public const int MinNameLength = 3;

    public static string PatternFor(string normalizedName) => $"^{Regex.Escape(normalizedName)}$";

    /// <param name="productName">Nazwa pozycji z paragonu (surowa — normalizuje się tu).</param>
    /// <param name="category">Wybrana kategoria; <c>null</c> = użytkownik zdjął kategorię.</param>
    public async Task LearnAsync(string productName, ProductCategory? category, CancellationToken ct)
    {
        var normalized = ProductNameNormalizer.Normalize(productName);
        if (normalized.Length < MinNameLength) return;

        var pattern = PatternFor(normalized);
        var existing = await db.ProductRules
            .FirstOrDefaultAsync(r => r.Pattern == pattern && r.UserId == currentUser.UserId, ct);

        if (category is null)
        {
            if (existing is not null) db.ProductRules.Remove(existing);
            return;
        }

        if (existing is null)
        {
            db.ProductRules.Add(new ProductRule(category.Id, pattern, ProductRule.LearnedPriority, currentUser.UserId));
        }
        else
        {
            existing.ChangeCategory(category.Id);
        }
    }
}
