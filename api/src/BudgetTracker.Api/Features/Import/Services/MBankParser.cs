using System.Globalization;
using System.Text;
using BudgetTracker.Api.Features.Import.Exceptions;
using BudgetTracker.Api.Features.Import.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace BudgetTracker.Api.Features.Import.Services;

/// <summary>
/// Parser wyciągu mBanku. Format potwierdzony na realnym pliku (142 operacje, 1 miesiąc) —
/// bank go nie dokumentuje.
/// </summary>
/// <remarks>
/// Rozpoznane cechy formatu, wszystkie zaskakujące na pierwszy rzut oka:
/// <list type="bullet">
///   <item>kodowanie <b>CP1250</b>, bez BOM — jak PKO, nie UTF-8;</item>
///   <item>plik zaczyna kilkanaście linii metadanych klienta i rachunku, każda w innym kształcie —
///     jedyny stały punkt zaczepienia to nagłówek tabeli operacji, <see cref="ExpectedHeader"/>;</item>
///   <item>⚠️ <b>wiersz operacji to CSV wewnątrz CSV.</b> Zewnętrzny plik jest rozdzielany przecinkiem,
///     ale każdy wiersz operacji to JEDNO pole w cudzysłowie, wewnątrz którego mBank skleja kolumny
///     średnikiem — bo Tytuł operacji czasem zawiera przecinek (np. tytuł przelewu z adresem),
///     a wtedy eksporter cytuje CAŁY sklejony wiersz, nie tylko to jedno pole. Zewnętrzny CsvHelper
///     i tak poprawnie odescape'uje podwojone <c>""</c>, więc drugie parsowanie tego samego tekstu
///     (tym razem średnikiem) dostaje już normalne, pojedyncze cudzysłowy wokół Tytułu i Nadawcy/Odbiorcy;</item>
///   <item>numer konta kontrahenta jest cytowany APOSTROFEM (<c>'...'</c>), nie cudzysłowem — to nie jest
///     znak cytowania CSV, tylko literalny znak w treści pola. Nie jest to numer referencyjny operacji
///     (mBank takiego nie eksportuje), więc <see cref="ParsedRow.ExternalReference"/> zostaje puste —
///     deduplikacja i tak schodzi na fallback data+kwota+opis;</item>
///   <item>kwota i saldo mają spację jako separator tysięcy („2 453,86”) i przecinek jako dziesiętny —
///     bez usunięcia spacji <c>decimal.Parse</c> rzuca;</item>
///   <item>⚠️ wiersze idą chronologicznie (najstarsza operacja na górze), w odróżnieniu od PKO — ale nie
///     idealnie: dwa sąsiednie wiersze potrafią mieć zamienioną kolejność dat księgowania o jeden dzień
///     (obserwowane na realnym pliku, zapewne przez opóźnione księgowanie zaokrągleń „Na Twoje Cele”).
///     Dlatego <see cref="ParseAsync"/> jawnie sortuje wynik po dacie, zamiast ufać kolejności w pliku —
///     stabilnie (<c>OrderBy</c>), żeby nie zaburzyć kolejności operacji tego samego dnia.</item>
/// </list>
/// </remarks>
public sealed class MBankParser : IStatementParser
{
    public string BankKey => "mbank";

    /// <summary>Fragment pierwszej linii pliku — jedyny sygnał formatu, zanim zacznie się tabela operacji.</summary>
    private const string ExpectedSignature = "mBank";

    /// <summary>Pierwsza kolumna nagłówka tabeli operacji — metadane nad nim nie mają stałego kształtu.</summary>
    private const string ExpectedHeader = "#Data księgowania";

    /// <summary>Liczba kolumn po rozbiciu spakowanego wiersza średnikiem (ostatnia bywa pustym ogonem).</summary>
    private const int MinInnerColumns = 8;

    /// <summary>
    /// Rejestruje strony kodowe: .NET zna domyślnie tylko Unicode, więc bez tego CP1250 rzuca wyjątkiem.
    /// </summary>
    static MBankParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Nagłówek tabeli operacji jest wyszukiwany w strumieniu metadanych, bo jego pozycja (liczba
    /// linii nad nim) zależy od typu rachunku i nie jest stała. Pojedynczy uszkodzony wiersz operacji
    /// jest pomijany — wywalenie całego importu przez jedną linię byłoby gorsze niż utrata jednej
    /// transakcji, którą użytkownik i tak zobaczy jako różnicę w podsumowaniu.
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

        var firstRow = csv.Record ?? [];
        if (firstRow.Length == 0 || !firstRow[0].Contains(ExpectedSignature, StringComparison.OrdinalIgnoreCase))
        {
            throw new StatementFormatException("Import_UnrecognizedFormat");
        }

        var sawHeader = false;
        var rows = new List<ParsedRow>();

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            var outer = csv.Record;
            if (outer is null || outer.Length == 0) continue;

            if (!sawHeader)
            {
                if (string.Equals(outer[0].Trim(), ExpectedHeader, StringComparison.OrdinalIgnoreCase))
                {
                    sawHeader = true;
                }
                continue;
            }

            var row = TryParseRow(outer[0]);
            if (row is not null) rows.Add(row);
        }

        if (!sawHeader)
        {
            throw new StatementFormatException("Import_UnrecognizedFormat");
        }

        if (rows.Count == 0)
        {
            throw new StatementFormatException("Import_NoRows");
        }

        return rows.OrderBy(r => r.Date).ToList();
    }

    /// <summary>Rozbija spakowany wiersz operacji (jedno pole zewnętrznego CSV) na kolumny mBanku.</summary>
    /// <remarks>
    /// Opis składamy z Tytułu i Nadawcy/Odbiorcy — dla operacji kartowych/BLIK drugie pole to zwykle
    /// szum („TRANSAKCJA BLIK”), ale dla przelewów to jedyne miejsce z nazwą kontrahenta. Numer konta
    /// celowo pomijamy, tak jak PKO pomija numery rachunków — to szum, który i tak wyciąłby normalizator.
    /// </remarks>
    private static ParsedRow? TryParseRow(string packed)
    {
        if (string.IsNullOrWhiteSpace(packed)) return null;

        using var innerReader = new StringReader(packed);
        using var inner = new CsvParser(innerReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = false,
            BadDataFound = null,
            DetectColumnCountChanges = false,
        });

        if (!inner.Read()) return null;
        var cells = inner.Record;
        if (cells is null || cells.Length < MinInnerColumns) return null;

        if (!DateOnly.TryParseExact(cells[0].Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        if (!TryParseAmount(cells[6], out var amount)) return null;

        var type = cells[2].Trim();
        var title = cells[3].Trim();
        var counterparty = cells[4].Trim();

        var description = string.Join(' ', new[] { title, counterparty }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        if (string.IsNullOrWhiteSpace(description))
        {
            description = type;
        }

        return new ParsedRow(date, amount, description, type, null);
    }

    /// <summary>
    /// „2 453,86” → 2453.86. Spacja (separator tysięcy) i przecinek (dziesiętny) to konwencja mBanku,
    /// niezależna od kultury maszyny.
    /// </summary>
    private static bool TryParseAmount(string raw, out decimal amount)
    {
        var cleaned = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}
