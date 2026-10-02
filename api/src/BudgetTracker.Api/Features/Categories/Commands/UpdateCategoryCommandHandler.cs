using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Categories.Commands;

/// <summary>Zmienia nazwę i typ własnej kategorii; typ tylko dopóki kategoria nie jest używana.</summary>
public sealed class UpdateCategoryCommandHandler(
    AppDbContext db, CategoryLookup lookup, CategoryNames names, CategoryUsage usage)
{
    public async Task<CategoryResponseDto> HandleAsync(Guid id, CategoryRequestDto request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Type)) throw new CategoryTypeInvalidException();

        var category = await lookup.FindOwnAsync(id, ct);
        var name = await names.ValidatedAsync(request.Name, category, ct);

        if (request.Type != category.Type)
        {
            if (await usage.InUseAsync(category, ct)) throw new CategoryTypeLockedException();
            category.ChangeType(request.Type);
        }

        category.Rename(name);
        await db.SaveChangesAsync(ct);

        var counts = (await usage.CountsAsync(ct)).GetValueOrDefault(category.Id);
        return CategoryUsage.View(category, counts);
    }
}
