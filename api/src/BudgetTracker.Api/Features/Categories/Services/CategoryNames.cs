using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categories.Services;

/// <summary>Walidacja nazwy kategorii: niepusta, mieszcząca się w kolumnie, unikalna wśród widocznych kategorii.</summary>
public sealed class CategoryNames(AppDbContext db)
{
    /// <summary>Długość kolumny <c>Categories.Name</c>.</summary>
    public const int MaxLength = 100;

    /// <summary>Przycięta nazwa; <paramref name="self"/> to kategoria, której nazwa się zmienia (nie koliduje sama ze sobą).</summary>
    /// <remarks>
    /// ⚠️ Unikalność sprawdzana jest po widocznych kategoriach, czyli własnych i wspólnych — indeks w bazie widzi tylko
    /// jednego właściciela, więc zderzenia ze wspólną nazwą sam nie złapie. Porównanie ignoruje wielkość liter.
    /// </remarks>
    public async Task<string> ValidatedAsync(string? raw, Category? self, CancellationToken ct)
    {
        var name = raw?.Trim() ?? string.Empty;
        if (name.Length == 0) throw new CategoryNameRequiredException();
        if (name.Length > MaxLength) throw new CategoryNameTooLongException();

        var lower = name.ToLower();
        var selfId = self?.Id;
        var taken = await db.Categories.AnyAsync(c => c.Name.ToLower() == lower && c.Id != selfId, ct);
        return taken ? throw new CategoryNameTakenException() : name;
    }
}
