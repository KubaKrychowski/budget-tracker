using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>Stan konta oszczędnościowego i to, ile z niego jest już umownie wpłacone na cele.</summary>
public sealed class SavingsAccount(AppDbContext db, SavingsCategory savingsCategory)
{
    /// <summary>
    /// Stan konta oszczędnościowego — fallback: wpłaty minus wypłaty w kategorii „Oszczędności".
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Netto jest tu POPRAWNE, w odróżnieniu od werdyktu celu.</b> Tam wypłata nie mogła
    /// psuć dowodu, bo dowód mierzy dyscyplinę wpłacania. Tu mierzymy, ile pieniędzy LEŻY
    /// na koncie — a wypłacone naprawdę z niego zeszły.
    ///
    /// Przelew NA oszczędnościowe wychodzi z konta bieżącego, więc w bazie jest ujemny.
    /// Negacja poza zapytaniem — EF nie tłumaczy <c>-x.Sum(...)</c> w projekcji.
    /// </remarks>
    public async Task<decimal> BalanceAsync(IReadOnlyList<Guid> budgetIds, CancellationToken ct)
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
