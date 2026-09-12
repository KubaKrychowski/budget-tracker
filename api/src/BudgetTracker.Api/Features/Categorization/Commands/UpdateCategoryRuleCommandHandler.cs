using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>Zmienia regułę kategoryzacji w całości — ta sama postać żądania co przy tworzeniu.</summary>
public sealed class UpdateCategoryRuleCommandHandler(AppDbContext db, CategoryRuleLookup lookup)
{
    public async Task<CategoryRuleResponseDto> HandleAsync(Guid id, CategoryRuleRequestDto request, CancellationToken ct)
    {
        var rule = await lookup.FindAsync(id, ct);
        var category = await lookup.CategoryAsync(request.CategoryId, ct);
        CategoryRuleValidator.Validate(request);

        rule.Update(
            category.Id,
            request.Direction,
            request.Priority,
            CategoryRuleValidator.Trim(request.Pattern),
            CategoryRuleValidator.Trim(request.TransactionTypePattern),
            request.MinAmount,
            request.MaxAmount,
            CategoryRuleValidator.Trim(request.Note));

        await db.SaveChangesAsync(ct);

        return CategoryRuleViews.Of(rule, category);
    }
}
