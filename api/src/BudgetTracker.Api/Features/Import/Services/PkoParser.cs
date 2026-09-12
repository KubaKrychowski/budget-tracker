using System.Globalization;
using System.Text;
using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Exceptions;
using BudgetTracker.Api.Features.Import.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace BudgetTracker.Api.Features.Import.Services;

/// <summary>
/// Parser wyciągu PKO. Format potwierdzony na realnym pliku (1340 wierszy, 20 miesięcy),
/// nie na dokumentacji — bank jej nie publikuje.
/// </summary>
/// <remarks>
/// Rozpoznane cechy formatu:
/// <list type="bullet">
///   <item>kodowanie <b>CP1250</b>, bez BOM — nie UTF-8;</item>
///   <item>przecinek jako separator, wszystkie pola w cudzysłowach, końce linii CRLF;</item>
///   <item>13 kolumn, ale nagłówek ma tylko 7 — sześć ostatnich jest bezimiennych;</item>
///   <item>kwota z kropką dziesiętną i znakiem: ujemne = wydatek, zgodnie z naszą konwencją.</item>
///   <item>wiersze idą OD NAJNOWSZEGO — <see cref="ParseAsync"/> odwraca kolejność przed
///     zwróceniem, żeby reszta systemu (kolejność zapisu, `Id`) dostała chronologię rosnącą.</item>
/// </list>
///
/// <b>Najważniejsze:</b> opis nie ma stałej pozycji. Kolumny 7–13 zawierają pary
/// „Etykieta: wartość”, ale etykieta w danej kolumnie zmienia się zależnie od typu transakcji —
/// kolumna 7 to zwykle „Tytuł”, ale bywa „Rachunek odbiorcy”. Dlatego szukamy po PREFIKSIE,
/// nigdy po indeksie. Naiwny parser oparty na pozycji dawał śmieci dla części wierszy.
/// </remarks>
public sealed class PkoParser : IStatementParser
{
    public string BankKey => "pko";

    /// <summary>Nagłówek, po którym poznajemy, że to w ogóle wyciąg PKO.</summary>
    private const string ExpectedFirstColumn = "Data operacji";

    private const string TitleLabel = "Tytuł:";
    private const string LocationLabel = "Lokalizacja:";
    private const string RecipientLabel = "Nazwa odbiorcy:";
    private const string SenderLabel = "Nazwa nadawcy:";
    private const string ReferenceLabel = "Numer referencyjny:";

    /// <summary>Od tej kolumny zaczynają się pola etykietowane.</summary>
    private const int FirstLabelledColumn = 6;

    /// <summary>
    /// Rejestruje strony kodowe: .NET zna domyślnie tylko Unicode, więc bez tego CP1250 rzuca wyjątkiem,
    /// a wyciąg PKO jest właśnie w CP1250.
    /// </summary>
    static PkoParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <list type="bullet">
    /// <item>Nagłówek jest czytany ręcznie (<c>HasHeaderRecord = false</c>), bo 6 kolumn nie ma nazwy.</item>
    /// <item>Pojedynczy wiersz nie do odczytania jest pomijany (<c>BadDataFound = null</c> i <see cref="TryParseRow"/>
    /// zwracające <c>null</c>). Wywalenie całego importu przez jedną uszkodzoną linię byłoby gorsze niż utrata jednej
    /// transakcji, którą użytkownik i tak zobaczy jako różnicę w podsumowaniu.</item>
    /// <item>⚠️ PKO eksportuje NAJNOWSZE operacje NA GÓRZE pliku — potwierdzone na żywo przez sklejanie kolumny
    /// „Saldo po transakcji" wiersz po wierszu na prawdziwym wyciągu (saldo_wcześniejszego_wiersza =
    /// saldo_późniejszego + kwota_wcześniejszego, zgadzało się idealnie dla całych dni). Reszta systemu zakłada
    /// odwrotnie: rosnące <c>Id</c> (kolejność zapisu = kolejność w tej liście) to rosnąca chronologia — używa tego
    /// m.in. wykres „Stan budżetu" (<c>OrderBy(Date).ThenBy(Id)</c>) i lista ostatnich transakcji
    /// (<c>ThenByDescending(Id)</c> jako „nowsza w tym samym dniu"). Bez odwrócenia transakcje z jednego dnia trafiały
    /// do bazy w kolejności DOKŁADNIE odwrotnej do rzeczywistej — np. wypłata wynagrodzenia (pierwsza operacja dnia)
    /// dostawała NAJWYŻSZE <c>Id</c>, więc na wykresie wyglądała jak OSTATNIA, a saldo w międzyczasie potrafiło
    /// schodzić poniżej zera, mimo że w rzeczywistości nigdy tam nie było.</item>
    /// </list>
    /// </remarks>
    public async Task<IReadOnlyList<ParsedRow>> ParseAsync(Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content, Encoding.GetEncoding(1250));
        using var csv = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = false,
            BadDataFound = null,
            DetectColumnCountChanges = false,
        });

        if (!await csv.ReadAsync())
        {
            throw new StatementFormatException("Import_EmptyFile");
        }

        var header = csv.Record ?? [];
        if (header.Length == 0 || !header[0].Trim().Equals(ExpectedFirstColumn, StringComparison.OrdinalIgnoreCase))
        {
            throw new StatementFormatException("Import_UnrecognizedFormat");
        }

        var rows = new List<ParsedRow>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            var row = TryParseRow(csv.Record);
            if (row is not null) rows.Add(row);
        }

        if (rows.Count == 0)
        {
            throw new StatementFormatException("Import_NoRows");
        }

        rows.Reverse();

        return rows;
    }

    /// <summary>Jeden wiersz wyciągu → <see cref="ParsedRow"/>; <c>null</c>, gdy wiersza nie da się odczytać.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Kwota: kropka dziesiętna i kultura niezmienna — plik nie zmienia formatu zależnie od ustawień maszyny,
    /// więc parsowanie też nie może.</item>
    /// <item>Opis składamy z pól niosących treść (tytuł, lokalizacja, kontrahent). Numery rachunków, telefonów
    /// i kart celowo pomijamy — to szum, który i tak wyciąłby normalizator. Gdy żadne z tych pól nie ma treści,
    /// ostatnią deską ratunku jest typ operacji.</item>
    /// </list>
    /// </remarks>
    private static ParsedRow? TryParseRow(string[]? cells)
    {
        if (cells is null || cells.Length < FirstLabelledColumn) return null;

        if (!DateOnly.TryParseExact(cells[0].Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        if (!decimal.TryParse(cells[3].Trim(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var type = cells.Length > 2 ? cells[2].Trim() : string.Empty;

        var title = FindByLabel(cells, TitleLabel);
        var location = FindByLabel(cells, LocationLabel);
        var counterparty = FindByLabel(cells, RecipientLabel) ?? FindByLabel(cells, SenderLabel);
        var reference = FindByLabel(cells, ReferenceLabel);

        var description = string.Join(' ', new[] { title, location, counterparty }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        if (string.IsNullOrWhiteSpace(description))
        {
            description = type;
        }

        return new ParsedRow(date, amount, description, type, reference);
    }

    /// <summary>
    /// Szuka wartości po prefiksie etykiety w kolumnach opisowych, bez zakładania pozycji.
    /// Ścina wiodący apostrof — PKO stosuje excelowy escape („'Operacja:”).
    /// </summary>
    private static string? FindByLabel(string[] cells, string label)
    {
        for (var i = FirstLabelledColumn; i < cells.Length; i++)
        {
            var cell = (cells[i] ?? string.Empty).TrimStart('\'').Trim();
            if (cell.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                var value = cell[label.Length..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        return null;
    }
}
