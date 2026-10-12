using BudgetTracker.Api.Features.Receipts.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Porównanie sumy paragonu z sumą pozycji — sama reguła, bez Azure. Kwoty są zmyślone.
/// </summary>
public sealed class ReceiptTotalCheckTests
{
    private static readonly decimal[] Items = [4.50m, 3.20m, 12.90m];

    [Fact]
    public void A_total_that_matches_the_items_keeps_its_confidence()
    {
        Assert.Equal(0.99f, ReceiptTotalCheck.Adjust(20.60m, 0.99f, Items));
    }

    [Fact]
    public void A_total_off_by_a_rounding_cent_still_keeps_its_confidence()
    {
        Assert.Equal(0.99f, ReceiptTotalCheck.Adjust(20.61m, 0.99f, Items));
    }

    [Fact]
    public void A_truncated_total_is_downgraded_despite_the_models_high_confidence()
    {
        // Łapie dokładnie przypadek z prawdziwego Azure: suma 20 przy pewności 0,993, a pozycje dają 20,60.
        Assert.Equal(ReceiptTotalCheck.MismatchConfidence, ReceiptTotalCheck.Adjust(20m, 0.993f, Items));
    }

    [Fact]
    public void A_mismatch_never_raises_an_already_low_confidence()
    {
        Assert.Equal(0.2f, ReceiptTotalCheck.Adjust(20m, 0.2f, Items));
    }

    [Fact]
    public void Without_items_there_is_nothing_to_compare_with()
    {
        Assert.Equal(0.99f, ReceiptTotalCheck.Adjust(20m, 0.99f, []));
    }

    [Fact]
    public void Without_a_total_the_confidence_is_passed_through()
    {
        Assert.Null(ReceiptTotalCheck.Adjust(null, null, Items));
    }
}
