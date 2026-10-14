using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.ProductCategories.Contracts;
using BudgetTracker.Api.Features.ProductCategories.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.ProductCategories.Commands;

/// <summary>Dodaje własną kategorię produktu zalogowanego konta.</summary>
/// <remarks>
/// ⚠️ Nazwa jest unikalna wśród własnych ORAZ wspólnych (bez względu na wielkość liter): indeks częściowy nie widzi wierszy innego właściciela,
/// więc zderzenie ze wspólną pilnuje ten handler.
/// </remarks>
public sealed class CreateProductCategoryCommandHandler(AppDbContext db, ICurrentUserAccessor currentUser)
{
    public async Task<ProductCategoryResponseDto> HandleAsync(ProductCategoryRequestDto request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ProductCategoryNameRequiredException();
        if (name.Length > ProductCategory.MaxNameLength) throw new ProductCategoryNameTooLongException();

        var lower = name.ToLowerInvariant();
        if (await db.ProductCategories.AnyAsync(c => c.Name.ToLower() == lower, ct))
        {
            throw new ProductCategoryNameTakenException();
        }

        var category = new ProductCategory(name, currentUser.UserId);
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(ct);

        return new ProductCategoryResponseDto(category.BusinessId, category.Name, category.IsShared);
    }
}
