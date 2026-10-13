using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categories.Commands;

/// <summary>Ukrywa kategorię wspólną dla zalogowanego konta (zgłoszenie #30); idempotentne.</summary>
/// <remarks>
/// Ukrywa się tylko kategorię WSPÓLNĄ (<see cref="CategoryHideNotSharedException"/>, 409 — stan zasobu nie pozwala). Kategoria używana
/// przez limity, reguły i transakcje może być ukryta: nic się nie kasuje, tylko znika z list wyboru i z podpowiedzi.
/// Ponowne ukrycie tej samej kategorii nic nie robi, więc podwójne kliknięcie nie kończy się błędem.
/// </remarks>
public sealed class HideCategoryCommandHandler(AppDbContext db, CategoryUsage usage, ICurrentUserAccessor currentUser)
{
    public async Task<CategoryResponseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.BusinessId == id, ct)
            ?? throw new CategoryNotFoundException(id);
        if (!category.IsShared) throw new CategoryHideNotSharedException(id);

        if (!await db.HiddenCategories.AnyAsync(h => h.CategoryId == category.Id, ct))
        {
            db.HiddenCategories.Add(new HiddenCategory(currentUser.UserId, category.Id));
            await db.SaveChangesAsync(ct);
        }

        var counts = await usage.CountsAsync(ct);
        return CategoryUsage.View(category, counts.GetValueOrDefault(category.Id), hidden: true);
    }
}
