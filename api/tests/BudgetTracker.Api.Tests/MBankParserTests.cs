using System.Text;
using BudgetTracker.Api.Features.Import.Exceptions;
using BudgetTracker.Api.Features.Import.Models;
using BudgetTracker.Api.Features.Import.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Testy parsera na SYNTETYCZNYM fixture w CP1250 — struktura 1:1 z wyciągiem mBanku,
/// ale wymyślone dane. Realny wyciąg nie trafia do repozytorium.
///
/// Czyste testy: bez bazy, bez sieci.
/// </summary>
public class MBankParserTests
{
    private static readonly string FixturePath = Path.Combine("testdata", "mbank-sample.csv");

    private static async Task<IReadOnlyList<ParsedRow>> Parse()
    {
        await using var stream = File.OpenRead(FixturePath);
        return await new MBankParser().ParseAsync(stream, default);
    }

    [Fact]
    public async Task Skips_the_malformed_row_instead_of_failing_the_whole_import()
    {
        var rows = await Parse();

        // Fixture ma 9 wierszy operacji, z czego jeden ma niepoprawną datę księgowania.
        Assert.Equal(8, rows.Count);
        Assert.DoesNotContain(rows, r => r.Description.Contains("USZKODZONY"));
    }

    [Fact]
    public async Task Reads_cp1250_so_polish_characters_survive()
    {
        var rows = await Parse();

        // Gdyby parser zakładał UTF-8, w tym miejscu byłyby krzaki.
        Assert.Contains(rows, r => r.Description.Contains("ŚWIĘTOKRZYSKI"));
        Assert.Contains(rows, r => r.Description.Contains("WRZESIEŃ"));
        Assert.Contains(rows, r => r.TransactionType == "PRZELEW ZEWNĘTRZNY WYCHODZĄCY");
    }

    [Fact]
    public async Task Unpacks_the_semicolon_row_nested_inside_the_comma_delimited_field()
    {
        var rows = await Parse();

        var rent = rows.Single(r => r.Amount == -1500.00m);
        Assert.Contains("CZYNSZ ZA WRZESIEŃ", rent.Description);
        Assert.Contains("SPÓŁDZIELNIA TESTOWA", rent.Description);
        Assert.DoesNotContain("1111111111", rent.Description); // numer konta nie wchodzi do opisu
    }

    [Fact]
    public async Task Parses_amounts_with_a_space_as_the_thousands_separator()
    {
        var rows = await Parse();

        Assert.Equal(-1500.00m, rows.Single(r => r.Description.Contains("CZYNSZ")).Amount);
        Assert.Equal(5000.00m, rows.Single(r => r.Description.Contains("WYNAGRODZENIE")).Amount);
    }

    [Fact]
    public async Task Keeps_the_sign_so_expenses_stay_negative_and_income_positive()
    {
        var rows = await Parse();

        Assert.Equal(5000.00m, rows.Single(r => r.Description.Contains("WYNAGRODZENIE")).Amount);
        Assert.All(rows.Where(r => r.Description.Contains("SKLEP.TESTOWY.PL")), r => Assert.True(r.Amount < 0));
    }

    [Fact]
    public async Task Never_extracts_a_reference_number_since_mbank_export_has_none()
    {
        var rows = await Parse();

        // Numer konta kontrahenta (cytowany apostrofem) to nie numer referencyjny operacji —
        // deduplikacja zawsze schodzi na fallback data+kwota+opis.
        Assert.All(rows, r => Assert.Null(r.ExternalReference));
    }

    [Fact]
    public async Task Identical_rows_share_an_identity_key_so_the_handler_can_merge_them()
    {
        var rows = await Parse();
        var duplicates = rows.Where(r => r.Description.Contains("SKLEP.TESTOWY.PL")).ToList();

        Assert.Equal(2, duplicates.Count);
        Assert.Equal(duplicates[0].IdentityKey(), duplicates[1].IdentityKey());
    }

    [Fact]
    public async Task Reorders_a_locally_swapped_pair_of_booking_dates_into_chronological_order()
    {
        // W realnym pliku dwa sąsiednie wiersze potrafią mieć zamienioną kolejność dat księgowania
        // o jeden dzień. Fixture odtwarza to: „OPŁATA ZA KARTĘ” (09-07) stoi w pliku PO
        // „ZAKUP PRZY UŻYCIU KARTY” (09-08, ale z datą operacji 09-07). Parser musi je zamienić miejscami.
        var rows = await Parse();
        var list = rows.ToList();

        var fee = list.Single(r => r.Amount == -5.00m);
        var cardPurchase = list.Single(r => r.Amount == -100.00m);

        Assert.Equal(new DateOnly(2026, 9, 7), fee.Date);
        Assert.Equal(new DateOnly(2026, 9, 8), cardPurchase.Date);
        Assert.True(list.IndexOf(fee) < list.IndexOf(cardPurchase));

        var dates = list.Select(r => r.Date).ToList();
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
    }

    [Fact]
    public async Task Rejects_a_file_that_is_not_an_mbank_statement()
    {
        var foreign = new MemoryStream(Encoding.UTF8.GetBytes("id,name\n1,test\n"));

        var ex = await Assert.ThrowsAsync<StatementFormatException>(
            () => new MBankParser().ParseAsync(foreign, default));

        // Wyjątek niesie KLUCZ zasobu, nie gotowy tekst — tłumaczenie należy do warstwy HTTP.
        Assert.Equal("Import_UnrecognizedFormat", ex.ResourceKey);
    }

    [Fact]
    public async Task Rejects_an_empty_file()
    {
        var empty = new MemoryStream([]);

        var ex = await Assert.ThrowsAsync<StatementFormatException>(
            () => new MBankParser().ParseAsync(empty, default));

        Assert.Equal("Import_EmptyFile", ex.ResourceKey);
    }
}
