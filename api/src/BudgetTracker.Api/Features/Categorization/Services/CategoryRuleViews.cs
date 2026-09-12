using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Contracts;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>Reguła w postaci odpowiedzi — wspólne dla listy i odpowiedzi po zapisie.</summary>
public static class CategoryRuleViews
{
    /// <summary>Reguła z nazwą i publicznym identyfikatorem kategorii, na którą wskazuje.</summary>
    public static CategoryRuleResponseDto Of(CategoryRule rule, Category category) => new(
        rule.BusinessId,
        rule.Pattern,
        rule.TransactionTypePattern,
        rule.Direction,
        category.BusinessId,
        category.Name,
        rule.Priority,
        rule.MinAmount,
        rule.MaxAmount,
        rule.Note);
}
