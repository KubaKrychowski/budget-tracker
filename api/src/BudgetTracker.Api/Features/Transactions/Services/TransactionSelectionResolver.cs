using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Services;

/// <summary>Zamienia <see cref="TransactionSelectionRequestDto"/> akcji masowej na listę identyfikatorów wierszy.</summary>
public sealed class TransactionSelectionResolver(TransactionBudgetScope scope, TransactionFilters filters)
{
    /// <summary>
    /// Rozstrzyga zasięg akcji masowej. Filtr wyznacza wszechświat (przede wszystkim BUDŻET),
    /// a jawne identyfikatory tylko go zawężają — patrz <see cref="TransactionSelectionRequestDto"/>.
    /// </summary>
    /// <remarks>
    /// Identyfikator spoza budżetu nie jest po cichu pomijany, tylko kończy się 404: skoro
    /// klient NAZWAŁ konkretne wiersze, to rozbieżność jest błędem, a nie sytuacją do
    /// przemilczenia. Ta sama reguła co przy edycji inline — jedna operacja nie może
    /// na ten sam przypadek raz odpowiadać 404, a raz „zmieniono 0 wierszy".
    /// </remarks>
    public async Task<List<Guid>> ResolveAsync(TransactionSelectionRequestDto selection, CancellationToken ct)
    {
        if (selection.Filter is not { } filter) return [];

        var budgetIds = TransactionBudgetScope.Resolve(
            await scope.OptionsAsync(ct), filter.BudgetIds, filter.To ?? scope.Today());
        if (budgetIds.Count == 0) return [];

        var ofBudget = scope.TransactionsOf(budgetIds);

        if (selection.Ids is not { Count: > 0 } ids)
            return await filters.Apply(ofBudget, filter).Select(t => t.BusinessId).ToListAsync(ct);

        var resolved = await ofBudget
            .Where(t => ids.Contains(t.BusinessId))
            .Select(t => t.BusinessId)
            .ToListAsync(ct);

        if (resolved.Count != ids.Distinct().Count())
        {
            var missing = ids.Except(resolved).First();
            throw new TransactionNotFoundException(missing);
        }

        return resolved;
    }
}
