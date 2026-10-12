using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Features.Transactions.Services;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Queries;

/// <summary>Transakcje z importu, do których może pasować paragon o podanej sumie i dacie.</summary>
/// <remarks>
/// Zasięg budżetów jak na liście transakcji (<see cref="TransactionBudgetScope"/>): bez <c>budgetId</c> szukamy w budżecie
/// domyślnym. Brak sumy albo daty = pusta lista, bo nie ma po czym dopasować.
/// </remarks>
public sealed class GetReceiptCandidatesQueryHandler(TransactionBudgetScope scope)
{
    public async Task<IReadOnlyList<ReceiptCandidateResponseDto>> HandleAsync(
        IReadOnlyList<Guid>? budgetIds, decimal? total, DateOnly? date, CancellationToken ct)
    {
        if (total is not > 0 || date is null) return [];

        var budgets = await scope.OptionsAsync(ct);
        var resolved = TransactionBudgetScope.Resolve(budgets, budgetIds, scope.Today());

        var from = date.Value.AddDays(-ReceiptCandidateFinder.DateWindowDays);
        var to = date.Value.AddDays(ReceiptCandidateFinder.DateWindowDays);
        var window = await scope.TransactionsOf(resolved)
            .Where(t => t.Amount < 0 && t.Date >= from && t.Date <= to)
            .ToListAsync(ct);

        return ReceiptCandidateFinder.Rank(window, total.Value, date.Value);
    }
}
