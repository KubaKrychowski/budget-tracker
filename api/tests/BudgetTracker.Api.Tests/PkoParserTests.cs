using System.Text;
using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Exceptions;
using BudgetTracker.Api.Features.Import.Models;
using BudgetTracker.Api.Features.Import.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Testy parsera na SYNTETYCZNYM fixture w CP1250 — struktura 1:1 z wyciągiem PKO,
/// ale wymyślone dane. Realny wyciąg nie trafia do repozytorium.
///
/// Czyste testy: bez bazy, bez sieci.
/// </summary>
public class PkoParserTests
{
    private static readonly string FixturePath = Path.Combine("testdata", "pko-sample.csv");

    private static async Task<IReadOnlyList<ParsedRow>> Parse()
    {
        await using var stream = File.OpenRead(FixturePath);
        return await new PkoParser().ParseAsync(stream, default);
    }

    [Fact]
    public async Task Skips_the_malformed_row_instead_of_failing_the_whole_import()
    {
        var rows = await Parse();

        // Fixture ma 8 wierszy danych, z czego jeden ma niepoprawną datę.
        Assert.Equal(7, rows.Count);
        Assert.DoesNotContain(rows, r => r.Description.Contains("USZKODZONY"));
    }

    [Fact]
    public async Task Reads_cp1250_so_polish_characters_survive()
    {
        var rows = await Parse();

        // Gdyby parser zakładał UTF-8, w tym miejscu byłyby krzaki.
        Assert.Contains(rows, r => r.Description.Contains("WRZESIEŃ"));
        Assert.Contains(rows, r => r.Description.Contains("ŻÓŁTA"));
        Assert.Contains(rows, r => r.TransactionType == "Płatność kartą");
    }

    [Fact]
    public async Task Finds_labels_by_prefix_not_by_column_position()
    {
        var rows = await Parse();

        // W wierszu z czynszem „Tytuł” siedzi w INNEJ kolumnie niż w wierszu ze sklepem.
        // Parser oparty na pozycji wyciągnąłby tu numer rachunku zamiast tytułu.
        var rent = rows.Single(r => r.Amount == -2100.00m);
        Assert.Contains("CZYNSZ ZA WRZESIEŃ", rent.Description);
        Assert.Contains("JAN KOWALCZYK", rent.Description);
        Assert.DoesNotContain("2222 3333", rent.Description);   // numer rachunku nie wchodzi do opisu
    }

    [Fact]
    public async Task Keeps_the_sign_so_expenses_stay_negative_and_income_positive()
    {
        var rows = await Parse();

        Assert.Equal(8500.00m, rows.Single(r => r.Description.Contains("WYNAGRODZENIE")).Amount);
        Assert.All(rows.Where(r => r.Description.Contains("MARKET")), r => Assert.True(r.Amount < 0));
    }

    [Fact]
    public async Task Extracts_reference_number_when_present_and_leaves_it_null_otherwise()
    {
        var rows = await Parse();

        Assert.Equal("REF00000001", rows.Single(r => r.Amount == -123.45m).ExternalReference);
        // Bilety komunikacyjne go nie mają — to one wymuszają fallback w kluczu deduplikacji.
        Assert.All(rows.Where(r => r.Amount == -4.20m), r => Assert.Null(r.ExternalReference));
    }

    [Fact]
    public async Task Strips_the_excel_apostrophe_escape()
    {
        var rows = await Parse();

        // PKO poprzedza część pól apostrofem („'Operacja:”). Nieobcięty trafiłby do opisu.
        Assert.DoesNotContain(rows, r => r.Description.StartsWith('\''));
    }

    [Fact]
    public async Task Atm_withdrawal_is_recognizable_only_by_transaction_type()
    {
        var rows = await Parse();
        var atm = rows.Single(r => r.Amount == -400.00m);

        // W opisie jest sam adres bankomatu — tekstowo nie do odróżnienia od zakupu.
        Assert.Equal("Wypłata w bankomacie - kod mobilny", atm.TransactionType);
        Assert.Contains("OBCY", atm.Description);
    }

    [Fact]
    public async Task Identical_rows_share_an_identity_key_so_the_handler_can_merge_them()
    {
        var rows = await Parse();
        var tickets = rows.Where(r => r.Amount == -4.20m).ToList();

        Assert.Equal(2, tickets.Count);
        Assert.Equal(tickets[0].IdentityKey(), tickets[1].IdentityKey());
    }

    [Fact]
    public async Task Reverses_pkos_newest_first_export_into_chronological_order()
    {
        // PKO eksportuje od NAJNOWSZEJ operacji (fixture: 31.08 na górze, 03.08 na dole).
        // Kolejność zwrócona z parsera musi być odwrotna — reszta systemu (kolejność zapisu,
        // `Id`) zakłada rosnącą chronologię. Sprawdzone na prawdziwym wyciągu (zgłoszenie
        // usera): bez tego wypłata wynagrodzenia — pierwsza operacja dnia — dostawała
        // NAJWYŻSZE `Id` w tym dniu i na wykresie wyglądała jak ostatnia, więc saldo
        // międzyczasie potrafiło pokazać wartości ujemne, których w rzeczywistości nigdy nie było.
        var rows = await Parse();

        var dates = rows.Select(r => r.Date).ToList();
        var chronological = dates.OrderBy(d => d).ToList();
        Assert.Equal(chronological, dates);

        // Najstarszy wiersz z fixture'a (03.08) ma być pierwszy, najnowszy (31.08) — ostatni.
        Assert.Equal(new DateOnly(2026, 8, 3), rows[0].Date);
        Assert.Equal(new DateOnly(2026, 8, 31), rows[^1].Date);
    }

    [Fact]
    public async Task Rejects_a_file_that_is_not_a_pko_statement()
    {
        var foreign = new MemoryStream(Encoding.UTF8.GetBytes("id,name\n1,test\n"));

        var ex = await Assert.ThrowsAsync<StatementFormatException>(
            () => new PkoParser().ParseAsync(foreign, default));

        // Wyjątek niesie KLUCZ zasobu, nie gotowy tekst — tłumaczenie należy do warstwy HTTP.
        Assert.Equal("Import_UnrecognizedFormat", ex.ResourceKey);
    }

    [Fact]
    public async Task Rejects_an_empty_file()
    {
        var empty = new MemoryStream([]);

        var ex = await Assert.ThrowsAsync<StatementFormatException>(
            () => new PkoParser().ParseAsync(empty, default));

        Assert.Equal("Import_EmptyFile", ex.ResourceKey);
    }
}
