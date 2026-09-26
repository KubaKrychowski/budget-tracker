using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>Zmienia powiązanie budżetu z budżetem oszczędnościowym i jego reguły transferu.</summary>
public sealed class UpdateSavingsLinkCommandHandler(AppDbContext db, BudgetLookup lookup, SavingsTransferMatcher matcher)
{
    /// <remarks>
    /// <list type="bullet">
    /// <item>Zdjęcie powiązania (<c>LinkedSavingsBudgetId = null</c>) czyści reguły i zapomina o ręcznych
    /// odpięciach (<see cref="SavingsTransferMatcher.ForgetAsync"/>) — inaczej ponowne powiązanie z INNYM
    /// budżetem odziedziczyłoby odpięcia, które go nie dotyczą.</item>
    /// <item>Zmiana i przeliczenie przypięć w jednej transakcji na żądanie (zakłada ją RlsTransactionEndpointFilter), wzorem zleceń stałych.</item>
    /// </list>
    /// </remarks>
    /// <exception cref="SavingsLinkTargetInvalidException">
    /// Wskazany budżet oszczędnościowy nie istnieje albo wskazuje sam na siebie.
    /// </exception>
    public async Task<SavingsLinkSavedResponseDto> HandleAsync(
        Guid businessId, UpdateSavingsLinkRequestDto request, CancellationToken ct)
    {
        var budget = await lookup.FindIncludingDeletedAsync(businessId, ct);
        var previousLink = budget.LinkedSavingsBudgetBusinessId;

        if (request.LinkedSavingsBudgetId is { } targetId)
        {
            if (targetId == businessId
                || !await db.Budgets.AnyAsync(b => b.BusinessId == targetId, ct))
            {
                throw new SavingsLinkTargetInvalidException();
            }

            var rules = SavingsTransferRuleValidator.Validate(request.Rules);
            budget.LinkSavingsBudget(targetId);
            budget.ReplaceSavingsTransferRules(rules);
        }
        else
        {
            budget.LinkSavingsBudget(null);
            budget.ReplaceSavingsTransferRules([]);
        }

        await db.SaveChangesAsync(ct);

        if (previousLink is { } old && previousLink != request.LinkedSavingsBudgetId)
        {
            await matcher.ForgetAsync(budget.BusinessId, old, ct);
        }

        var linked = await matcher.RematchAsync(budget, ct);

        return new SavingsLinkSavedResponseDto(budget.LinkedSavingsBudgetBusinessId, linked);
    }
}
