using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Dashboard.Consts;
using BudgetTracker.Api.Features.Dashboard.Contracts;

namespace BudgetTracker.Api.Features.Dashboard.Services;

/// <summary>Rozbija wydatki jednej kategorii na kategorie produktów z paragonów — czysta funkcja, bez bazy.</summary>
/// <remarks>
/// <para>
/// ⚠️ Rozbicie ZAWSZE sumuje się do kwoty kategorii, bo wykres pokazuje je jako część słupka. Stąd trzy reguły na transakcję:
/// </para>
/// <list type="bullet">
/// <item>bez paragonu z odczytanymi cenami pozycji cała kwota idzie do <see cref="ProductSpendKind.NoReceipt"/>;</item>
/// <item>gdy suma pozycji PRZEKRACZA kwotę transakcji (rabat naliczony po cenach pozycji), pozycje skaluje się proporcjonalnie w dół —
/// inaczej rozbicie wyszłoby większe niż słupek;</item>
/// <item>gdy suma pozycji jest MNIEJSZA (nieodczytana pozycja, opłata), różnica idzie do <see cref="ProductSpendKind.Uncategorized"/>.</item>
/// </list>
/// <para>Kwoty zaokrągla się dopiero po zsumowaniu wszystkich transakcji, więc suma wierszy może różnić się od kwoty o grosze.</para>
/// </remarks>
public static class ProductBreakdown
{
    /// <summary>Wydatek (dodatni) razem z pozycjami paragonów przypiętych do niego; brak paragonu = pusta lista.</summary>
    public sealed record SpentTransaction(decimal Amount, IReadOnlyList<ReceiptLine> Lines);

    /// <summary>Rozbicie wydatków jednej kategorii; pusta lista, gdy żadna transakcja nie ma użytecznych pozycji.</summary>
    /// <param name="productNames">Nazwy kategorii produktów po <c>BusinessId</c>; nieznana kategoria liczy się jak brak kategorii.</param>
    public static IReadOnlyList<ProductSpendResponseDto> For(
        IEnumerable<SpentTransaction> transactions, IReadOnlyDictionary<Guid, string> productNames)
    {
        var byProduct = new Dictionary<Guid, decimal>();
        decimal uncategorized = 0m, noReceipt = 0m;
        var anyLines = false;

        foreach (var tx in transactions)
        {
            var priced = tx.Lines.Where(l => l.TotalPrice is not null).ToList();
            var linesTotal = priced.Sum(l => l.TotalPrice!.Value);
            if (priced.Count == 0 || linesTotal <= 0m)
            {
                noReceipt += tx.Amount;
                continue;
            }

            anyLines = true;
            var scale = linesTotal > tx.Amount ? tx.Amount / linesTotal : 1m;
            foreach (var line in priced)
            {
                var amount = line.TotalPrice!.Value * scale;
                if (line.ProductCategoryId is { } id && productNames.ContainsKey(id))
                {
                    byProduct[id] = byProduct.GetValueOrDefault(id) + amount;
                }
                else
                {
                    uncategorized += amount;
                }
            }

            uncategorized += tx.Amount - linesTotal * scale;
        }

        if (!anyLines) return [];

        var rows = byProduct
            .Select(p => new ProductSpendResponseDto(p.Key, productNames[p.Key], Math.Round(p.Value, 2), ProductSpendKind.Category))
            .Where(r => r.Amount > 0m)
            .OrderByDescending(r => r.Amount)
            .ToList();

        if (Math.Round(uncategorized, 2) is var u and > 0m)
        {
            rows.Add(new ProductSpendResponseDto(null, null, u, ProductSpendKind.Uncategorized));
        }

        if (Math.Round(noReceipt, 2) is var n and > 0m)
        {
            rows.Add(new ProductSpendResponseDto(null, null, n, ProductSpendKind.NoReceipt));
        }

        return rows;
    }
}
