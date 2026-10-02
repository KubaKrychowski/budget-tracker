using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Categories.Commands;

/// <summary>Usuwa logicznie własną kategorię, która nie jest nigdzie używana.</summary>
/// <remarks>Kasowanie logiczne — <c>SoftDeleteInterceptor</c> zamienia <c>Remove</c> na ustawienie <c>DeletedAt</c>.</remarks>
public sealed class DeleteCategoryCommandHandler(AppDbContext db, CategoryLookup lookup, CategoryUsage usage)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var category = await lookup.FindOwnAsync(id, ct);
        if (await usage.InUseAsync(category, ct)) throw new CategoryInUseException();

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }
}
