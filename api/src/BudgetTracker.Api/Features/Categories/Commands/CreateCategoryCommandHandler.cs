using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Categories.Commands;

/// <summary>Dodaje własną kategorię zalogowanego konta.</summary>
public sealed class CreateCategoryCommandHandler(AppDbContext db, CategoryNames names, ICurrentUserAccessor currentUser)
{
    public async Task<CategoryResponseDto> HandleAsync(CategoryRequestDto request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Type)) throw new CategoryTypeInvalidException();

        var category = new Category(await names.ValidatedAsync(request.Name, self: null, ct), request.Type, currentUser.UserId);

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return CategoryUsage.View(category, (0, 0, 0));
    }
}
