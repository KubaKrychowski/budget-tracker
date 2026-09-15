using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>Stan konta oszczędnościowego i to, ile z niego jest już umownie wpłacone na cele.</summary>
public sealed class SavingsAccount(AppDbContext db, SavingsCategory savingsCategory)
{
    /// <summary>
    /// Stan konta oszczędnościowego. Dla budżetu z powiązanym budżetem oszczędnościowym (#10) —
    /// prawdziwy bilans TEGO budżetu (<c>InitialBalance</c> + suma jego transakcji, ta sama
    /// poprawna mechanika co dla każdego budżetu). Dla reszty — fallback: wpłaty minus wypłaty
    /// w kategorii „Oszczędności".
    /// </summary>
    /// <remarks>
    /// ⚠️ Fallback liczy netto ze znakiem — poprawnie DLA STANU (w odróżnieniu od werdyktu celu,
    /// który mierzy dyscyplinę wpłacania z samych wpłat), ale tylko wtedy, gdy kategoria złapała
    /// OBIE strony. Na realnych danych złapała wyłącznie wpłaty (reguła na słowo „przeniesien"
    /// pasuje do tytułu przelewu NA oszczędnościowe, nie do wypłat opisanych inaczej) — licznik rósł
    /// bez końca, bo nigdy nie widział wypłat. Budżet z prawdziwym importem konta oszczędnościowego
    /// nie ma tego problemu: liczy się bilans, nie zgadywanie po opisie.
    /// </remarks>
    public async Task<decimal> BalanceAsync(IReadOnlyList<Guid> budgetIds, CancellationToken ct)
    {
        if (budgetIds.Count == 0) return 0m;

        var links = await db.Budgets
            .Where(b => budgetIds.Contains(b.BusinessId))
            .Select(b => b.LinkedSavingsBudgetBusinessId)
            .ToListAsync(ct);

        var linkedIds = links.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var unlinkedCount = links.Count(id => id is null);

        var linkedBalance = linkedIds.Count == 0 ? 0m : await LinkedBudgetsBalanceAsync(linkedIds, ct);
        var fallbackBalance = unlinkedCount == 0 ? 0m : await CategoryFallbackBalanceAsync(budgetIds, ct);

        return linkedBalance + fallbackBalance;
    }

    /// <summary>Prawdziwy bilans powiązanych budżetów oszczędnościowych — jak dla każdego innego budżetu.</summary>
    private async Task<decimal> LinkedBudgetsBalanceAsync(IReadOnlyList<Guid> linkedBudgetIds, CancellationToken ct)
    {
        var budgets = await db.Budgets
            .Where(b => linkedBudgetIds.Contains(b.BusinessId))
            .Select(b => new { b.BusinessId, b.InitialBalance })
            .ToListAsync(ct);

        var booked = await db.Transactions
            .Where(t => t.BudgetBusinessId != null && linkedBudgetIds.Contains(t.BudgetBusinessId.Value))
            .GroupBy(t => t.BudgetBusinessId)
            .Select(g => new { BudgetBusinessId = g.Key!.Value, Sum = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        return budgets.Sum(b =>
            b.InitialBalance + (booked.FirstOrDefault(s => s.BudgetBusinessId == b.BusinessId)?.Sum ?? 0m));
    }

    /// <summary>
    /// Stary fallback dla budżetów BEZ powiązania — wpłaty minus wypłaty w kategorii „Oszczędności".
    /// Zakres liczy się po WSZYSTKICH <paramref name="budgetIds"/>, nie tylko niepowiązanych: dawne
    /// zapytanie i tak filtrowało po kategorii, więc powiązany budżet bez transakcji tej kategorii
    /// wnosi zero — nie trzeba osobno zawężać listy.
    /// </summary>
    private async Task<decimal> CategoryFallbackBalanceAsync(IReadOnlyList<Guid> budgetIds, CancellationToken ct)
    {
        var savingsCategoryId = await savingsCategory.IdAsync(ct);
        if (savingsCategoryId is null) return 0m;

        var signed = await db.Transactions
            .Where(t => t.BudgetBusinessId != null && budgetIds.Contains(t.BudgetBusinessId.Value)
                        && t.CategoryId == savingsCategoryId)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        return -signed;
    }

    /// <summary>Wpłaty na rezerwacje nierozliczone — rozliczonych już nie liczymy, te pieniądze wyszły z konta.</summary>
    /// <remarks>Suma w pamięci: wpłaty siedzą w kolumnie jsonb, a rezerwacji w budżecie jest z natury kilka.</remarks>
    public async Task<decimal> ContributedAsync(IReadOnlyList<Guid> budgetIds, CancellationToken ct) =>
        (await db.SavingsReservations
            .Where(r => budgetIds.Contains(r.BudgetBusinessId) && r.SettledAt == null)
            .ToListAsync(ct))
        .Sum(r => r.Contributed);
}
