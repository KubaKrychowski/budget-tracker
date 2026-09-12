using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Import.Services;

/// <summary>Klucze tożsamości transakcji, które JUŻ SĄ w budżecie — podstawa deduplikacji importu.</summary>
public sealed class ExistingTransactionKeys(AppDbContext db)
{
    /// <summary>Klucze transakcji budżetu z zakresu dat wierszy <paramref name="rows"/>.</summary>
    /// <remarks>
    /// ⚠️ Zakres jest per budżet, nie globalny. Budżet to zbiór zasad, na których pracują
    /// procesy wokół transakcji, a kilka budżetów istnieje właśnie po to, żeby porównywać
    /// warianty TYCH SAMYCH danych. Przy globalnej deduplikacji drugi wariant przychodziłby
    /// pusty — każdy wiersz byłby „duplikatem" wiersza z pierwszego budżetu.
    ///
    /// Kandydatów pobieramy jednym zapytaniem po zakresie dat, a nie per wiersz — import to setki pozycji
    /// i N+1 byłoby tu bolesne.
    /// </remarks>
    public async Task<HashSet<string>> LoadAsync(
        IReadOnlyList<ParsedRow> rows, Guid budgetBusinessId, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var from = rows.Min(r => r.Date);
        var to = rows.Max(r => r.Date);

        var candidates = await db.Transactions
            .Where(t => t.BudgetBusinessId == budgetBusinessId && t.Date >= from && t.Date <= to)
            .Select(t => new { t.Date, t.Amount, t.Description, t.ExternalReference })
            .ToListAsync(ct);

        return candidates
            .Select(t => new ParsedRow(t.Date, t.Amount, t.Description, string.Empty, t.ExternalReference).IdentityKey())
            .ToHashSet();
    }
}
