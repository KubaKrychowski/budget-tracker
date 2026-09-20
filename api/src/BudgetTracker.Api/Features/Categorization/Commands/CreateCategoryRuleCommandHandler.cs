using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>Dodaje regułę kategoryzacji — działa od następnego żądania, bez restartu (patrz <see cref="RuleCategorizer"/>).</summary>
public sealed class CreateCategoryRuleCommandHandler(AppDbContext db, CategoryRuleLookup lookup, ICurrentUserAccessor currentUserAccessor)
{
    public async Task<CategoryRuleResponseDto> HandleAsync(CategoryRuleRequestDto request, CancellationToken ct)
    {
        var category = await lookup.CategoryAsync(request.CategoryId, ct);
        CategoryRuleValidator.Validate(request);

        var rule = new CategoryRule(
            category.Id,
            request.Direction,
            request.Priority,
            currentUserAccessor.UserId,
            CategoryRuleValidator.Trim(request.Pattern),
            CategoryRuleValidator.Trim(request.TransactionTypePattern),
            request.MinAmount,
            request.MaxAmount,
            CategoryRuleValidator.Trim(request.Note));

        db.Set<CategoryRule>().Add(rule);
        await db.SaveChangesAsync(ct);

        return CategoryRuleViews.Of(rule, category);
    }
}
