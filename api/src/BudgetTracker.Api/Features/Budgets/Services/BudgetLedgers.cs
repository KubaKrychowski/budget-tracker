using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>
/// Wczytuje transakcje budżetu i buduje z nich <see cref="BalanceLedger"/> — JEDYNA droga do bilansu budżetu.
/// </summary>
/// <remarks>
/// ⚠️ Dashboard, lista budżetów, podsumowanie importu i konto oszczędnościowe pokazują ten sam bilans, więc liczą go tu, a nie
/// każdy po swojemu: dwa ekrany z dwiema różnymi kwotami dla tego samego budżetu to błąd, którego użytkownik nie umie zdiagnozować.
/// Filtr właściciela i soft delete działają jak w każdym zapytaniu na <c>db.Transactions</c>.
/// </remarks>
public static class BudgetLedgers
{
    /// <summary>Księga jednego budżetu; <paramref name="budgetBusinessId"/> = <c>null</c> to transakcje bez budżetu (dane sprzed pola budżetu).</summary>
    public static async Task<BalanceLedger> LoadAsync(
        AppDbContext db, Guid? budgetBusinessId, decimal initialBalance, CancellationToken ct)
    {
        var rows = await db.Transactions
            .Where(t => t.BudgetBusinessId == budgetBusinessId)
            .Select(t => new LedgerInput(t.Id, t.Date, t.Amount, t.BalanceAfter))
            .ToListAsync(ct);

        return BalanceLedger.Build(initialBalance, rows);
    }

    /// <summary>Księgi wielu budżetów jednym zapytaniem — bez zapytania na budżet.</summary>
    public static async Task<IReadOnlyDictionary<Guid, BalanceLedger>> LoadAsync(
        AppDbContext db, IReadOnlyCollection<(Guid BusinessId, decimal InitialBalance)> budgets, CancellationToken ct)
    {
        var ids = budgets.Select(b => b.BusinessId).ToList();

        var rows = await db.Transactions
            .Where(t => t.BudgetBusinessId != null && ids.Contains(t.BudgetBusinessId.Value))
            .Select(t => new { BudgetBusinessId = t.BudgetBusinessId!.Value, t.Id, t.Date, t.Amount, t.BalanceAfter })
            .ToListAsync(ct);

        var byBudget = rows.ToLookup(r => r.BudgetBusinessId);

        return budgets.ToDictionary(
            b => b.BusinessId,
            b => BalanceLedger.Build(
                b.InitialBalance,
                byBudget[b.BusinessId].Select(r => new LedgerInput(r.Id, r.Date, r.Amount, r.BalanceAfter))));
    }
}
