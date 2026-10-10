using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Receipts.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Dopasowanie paragonu do transakcji z importu — sama reguła, bez bazy. Opisy i kwoty są zmyślone.
/// </summary>
public sealed class ReceiptCandidateFinderTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);

    private static Transaction Tx(DateOnly date, decimal amount, string description = "ZAKUP KARTA") =>
        new(date, amount, description, Now, TransactionStatus.Imported);

    [Fact]
    public void An_exact_amount_on_the_same_day_is_an_exact_match()
    {
        var result = ReceiptCandidateFinder.Rank([Tx(Day, -20.60m)], 20.60m, Day);

        Assert.Equal("Exact", Assert.Single(result).Match);
    }

    [Fact]
    public void A_close_but_different_amount_is_only_possible_and_ranks_after_the_exact_one()
    {
        var exact = Tx(Day.AddDays(2), -20.60m, "DOKLADNA");
        var close = Tx(Day, -21.00m, "ZBLIZONA");

        var result = ReceiptCandidateFinder.Rank([close, exact], 20.60m, Day);

        // Łapie błąd, w którym bliższa DATA wygrywała z dokładną KWOTĄ.
        Assert.Equal(["Exact", "Possible"], result.Select(r => r.Match));
        Assert.Equal(["DOKLADNA", "ZBLIZONA"], result.Select(r => r.Description));
    }

    [Fact]
    public void A_transaction_outside_the_date_window_is_not_a_candidate()
    {
        var result = ReceiptCandidateFinder.Rank(
            [Tx(Day.AddDays(ReceiptCandidateFinder.DateWindowDays + 1), -20.60m)], 20.60m, Day);

        Assert.Empty(result);
    }

    [Fact]
    public void An_amount_far_from_the_total_is_not_a_candidate()
    {
        Assert.Empty(ReceiptCandidateFinder.Rank([Tx(Day, -35.00m)], 20.60m, Day));
    }

    [Fact]
    public void Income_is_never_a_candidate_even_with_the_same_absolute_amount()
    {
        // Paragon dokumentuje zakup — wpływ o tej samej kwocie nie może być zaproponowany.
        Assert.Empty(ReceiptCandidateFinder.Rank([Tx(Day, 20.60m, "ZWROT")], 20.60m, Day));
    }

    [Fact]
    public void Only_the_best_few_candidates_are_returned()
    {
        var many = Enumerable.Range(0, 9).Select(i => Tx(Day, -20.60m - i * 0.10m)).ToList();

        Assert.Equal(ReceiptCandidateFinder.MaxCandidates, ReceiptCandidateFinder.Rank(many, 20.60m, Day).Count);
    }
}
