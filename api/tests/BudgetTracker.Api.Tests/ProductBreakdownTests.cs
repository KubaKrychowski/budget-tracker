using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Dashboard.Consts;
using BudgetTracker.Api.Features.Dashboard.Services;
using Xunit;
using static BudgetTracker.Api.Features.Dashboard.Services.ProductBreakdown;

namespace BudgetTracker.Api.Tests;

/// <summary>Rozbicie kategorii na produkty z paragonów — czysta funkcja, bez bazy. Kwoty i nazwy są zmyślone.</summary>
public sealed class ProductBreakdownTests
{
    private static readonly Guid Dairy = Guid.NewGuid();
    private static readonly Guid Bakery = Guid.NewGuid();

    private static readonly Dictionary<Guid, string> Names = new() { [Dairy] = "Nabiał", [Bakery] = "Pieczywo" };

    private static ReceiptLine Line(decimal price, Guid? category = null) => new("Produkt", 1m, price, price, category);

    private static decimal Sum(IReadOnlyList<Features.Dashboard.Contracts.ProductSpendResponseDto> rows) => rows.Sum(r => r.Amount);

    [Fact]
    public void Lines_are_grouped_by_product_category_and_the_rest_is_uncategorized()
    {
        var rows = For([new SpentTransaction(30m, [Line(10m, Dairy), Line(5m, Dairy), Line(10m, Bakery), Line(5m)])], Names);

        Assert.Equal(
            [("Nabiał", 15m, ProductSpendKind.Category), ("Pieczywo", 10m, ProductSpendKind.Category), (null, 5m, ProductSpendKind.Uncategorized)],
            rows.Select(r => (r.Name, r.Amount, r.Kind)));
    }

    [Fact]
    public void A_transaction_without_a_receipt_goes_to_the_no_receipt_row()
    {
        var rows = For([new SpentTransaction(30m, [Line(30m, Dairy)]), new SpentTransaction(20m, [])], Names);

        Assert.Equal(20m, rows.Single(r => r.Kind == ProductSpendKind.NoReceipt).Amount);
        Assert.Equal(50m, Sum(rows));
    }

    [Fact]
    public void Lines_that_exceed_the_transaction_are_scaled_down_so_the_breakdown_never_outgrows_the_bar()
    {
        // Łapie rabat naliczony po cenach pozycji: pozycje 40 zł przy płatności 30 zł dawały rozbicie większe niż słupek.
        var rows = For([new SpentTransaction(30m, [Line(20m, Dairy), Line(20m, Bakery)])], Names);

        Assert.Equal(30m, Sum(rows));
        Assert.Equal([15m, 15m], rows.Select(r => r.Amount));
    }

    [Fact]
    public void Lines_that_fall_short_of_the_transaction_put_the_difference_in_uncategorized()
    {
        var rows = For([new SpentTransaction(30m, [Line(20m, Dairy)])], Names);

        Assert.Equal(10m, rows.Single(r => r.Kind == ProductSpendKind.Uncategorized).Amount);
        Assert.Equal(30m, Sum(rows));
    }

    [Fact]
    public void Without_any_priced_line_there_is_no_breakdown_at_all()
    {
        // Pusta lista = front nie pokazuje strzałki rozwijania; sam „Bez paragonu” na 100% nic by nie mówił.
        Assert.Empty(For([new SpentTransaction(30m, []), new SpentTransaction(5m, [new ReceiptLine("Produkt", 1m, null, null)])], Names));
    }

    [Fact]
    public void A_category_id_missing_from_the_names_counts_as_uncategorized()
    {
        var rows = For([new SpentTransaction(10m, [Line(10m, Guid.NewGuid())])], Names);

        Assert.Equal(ProductSpendKind.Uncategorized, Assert.Single(rows).Kind);
    }
}
