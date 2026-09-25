using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.ML;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Składa zbiór treningowy z DWÓCH źródeł: pliku bazowego i poprawek kategorii z bazy.
/// </summary>
/// <remarks>
/// To jest cała istota zadania. Wcześniej trening czytał wyłącznie plik, więc pętla uczenia
/// z CLAUDE.md §3 (model przewiduje → człowiek poprawia → poprawka uczy model) była przerwana
/// na ostatnim kroku: można było poprawiać kategorie bez końca, a model zostawał ten sam.
/// </remarks>
public sealed class TrainingSetBuilder(AppDbContext db, IOptions<CategorizationOptions> options, BlobServiceClient blobServiceClient, IConfiguration configuration)
{
    /// <summary>
    /// Statusy, które ZNACZĄ „tak zdecydował człowiek".
    /// </summary>
    /// <remarks>
    /// ⚠️ <see cref="TransactionStatus.AutoCategorized"/> jest tu nieobecny CELOWO i nie wolno
    /// go dodać. To wyjście modelu — uczenie na własnych predykcjach jest samopotwierdzeniem:
    /// utrwala błędy i zawyża metryki, bo model dostaje do sprawdzenia to, co sam powiedział.
    /// </remarks>
    private static readonly TransactionStatus[] HumanDecisions =
        [TransactionStatus.ManuallyCategorized, TransactionStatus.Confirmed];

    /// <summary>Plik bazowy uzupełniony o poprawki człowieka z bazy.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Wiersze pliku są indeksowane po kluczu jako LISTA, nie pojedynczy wiersz: ten sam zakup potrafi
    /// powtórzyć się w wyciągu, a każde wystąpienie waży w zbiorze osobno.</item>
    /// <item>Kolejność poprawek po <c>CreatedAt</c> jest CZĘŚCIĄ REGUŁY, nie kosmetyką: przy dwóch sprzecznych
    /// decyzjach o tym samym wierszu wygrywa NOWSZA. Bez jawnego porządku wynik zależałby od kolejności
    /// zwróconej przez bazę. <c>CreatedAt</c> jest też wynoszone do wyniku — handler liczy z niego
    /// „ile przybyło od ostatniego treningu" bez drugiego zapytania.</item>
    /// <item>Opis poprawki idzie PO normalizacji, tej samej co przy predykcji. Inaczej model uczyłby się
    /// na innym tekście, niż potem widzi — plik bazowy też trzyma opisy po normalizacji.</item>
    /// <item>Ten sam przykład Z TĄ SAMĄ etykietą to duplikat — plik bazowy powstał z tych samych realnych
    /// transakcji, więc bez odsiania wszedłby do zbioru po raz drugi i przeważył go.</item>
    /// <item>⚠️ RÓŻNA etykieta to NIE duplikat, tylko POPRAWKA — i to ona jest sednem całej pętli uczenia.
    /// Wcześniej lądowała w koszu razem z duplikatami, więc korekta wiersza, który już był w pliku, nie zmieniała
    /// w modelu absolutnie niczego. Człowiek ma tu ostatnie słowo, nie plik. Przeetykietowane zostaje KAŻDE
    /// wystąpienie tego klucza: wiersze o identycznych cechach i sprzecznych etykietach to dla modelu czysty
    /// szum, a nie dwie opinie.</item>
    /// <item>⚠️ Licznik przeetykietowanych rośnie TYLKO dla wierszy z pliku. Gdy nowsza poprawka nadpisuje
    /// starszą poprawkę z tego samego przebiegu (klucz, którego w pliku nie ma), ten wiersz jest już policzony
    /// jako nowy — doliczenie go także tutaj kazałoby ekranowi pokazać „1 nowy + 1 przeetykietowany" tam,
    /// gdzie powstał jeden przykład.</item>
    /// </list>
    /// </remarks>
    public async Task<TrainingSet> BuildAsync(CancellationToken ct)
    {
        var fromFile = await LoadBaseFileAsync(ct);

        var byKey = new Dictionary<(string, string, decimal), List<TransactionFeatures>>();
        foreach (var row in fromFile)
        {
            if (!byKey.TryGetValue(KeyOf(row), out var bucket)) byKey[KeyOf(row)] = bucket = [];
            bucket.Add(row);
        }

        var fileKeys = byKey.Keys.ToHashSet();

        var corrections = await QualifyingCorrections()
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Description,
                t.TransactionType,
                t.Amount,
                t.CreatedAt,
                CategoryName = db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefault()!,
            })
            .ToListAsync(ct);

        var added = new List<TransactionFeatures>();
        var duplicates = 0;
        var corrected = 0;

        foreach (var c in corrections)
        {
            var row = new TransactionFeatures
            {
                Description = DescriptionNormalizer.Normalize(c.Description),
                TransactionType = c.TransactionType,
                Amount = (float)c.Amount,
                Category = c.CategoryName,
            };

            var key = KeyOf(row);

            if (!byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = [row];
                added.Add(row);
                continue;
            }

            if (existing.All(e => string.Equals(e.Category, row.Category, StringComparison.Ordinal)))
            {
                duplicates++;
                continue;
            }

            foreach (var e in existing) e.Category = row.Category;

            if (fileKeys.Contains(key)) corrected++;
        }

        var rows = new List<TransactionFeatures>(fromFile);
        rows.AddRange(added);

        return new TrainingSet(
            rows,
            new TrainingSetCompositionResponseDto(fromFile.Count, added.Count, corrected, duplicates),
            [.. corrections.Select(c => c.CreatedAt)]);
    }

    /// <summary>
    /// Reguły kwalifikacji — najważniejsze miejsce w tym slice'ie.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>Amount &lt; 0</c> nie jest optymalizacją, tylko warunkiem poprawności. CLAUDE.md §3
    /// (rewizja 2026-09-02): model widzi WYŁĄCZNIE wydatki, wpływy obsługują reguły. Wpuszczenie
    /// wpływów przywraca dokładnie ten opisany tam błąd — wynagrodzenie klasyfikowane jako
    /// „Gastronomia" z pewnością ~1.0, czyli powyżej progu, więc taka transakcja nie trafia
    /// nawet do przeglądu. Wysoka pewność nie znaczy tam „trafione", tylko „źle zadane pytanie".
    /// </remarks>
    private IQueryable<Transaction> QualifyingCorrections() => db.Transactions
        .Where(t => t.Amount < 0)
        .Where(t => HumanDecisions.Contains(t.Status))
        .Where(t => t.CategoryId != null)
        .Where(t => t.Description != "");

    /// <summary>
    /// Wczytuje plik bazowy tym samym loaderem ML.NET, którym robił to trening — parsowanie
    /// (cudzysłowy, separatory, nagłówek) musi zostać identyczne, bo na tym pliku model
    /// nauczył się tego, co dziś potrafi.
    /// </summary>
    /// <remarks>
    /// ⚠️ Piąta kolumna pliku (<c>duzy_wydatek</c>) NIE jest wczytywana: <see cref="TransactionFeatures"/>
    /// deklaruje <c>LoadColumn(0..3)</c>. To nie jest przeoczenie tego kodu — flaga nigdy nie była
    /// cechą modelu, a dodanie jej byłoby zmianą cech, czyli strojeniem, nie utrzymaniem.
    /// </remarks>
    private async Task<List<TransactionFeatures>> LoadBaseFileAsync(CancellationToken ct)
    {
        var blob = blobServiceClient
            .GetBlobContainerClient(configuration["Storage:SharedContainerName"]!)
            .GetBlobClient(configuration["Storage:TrainingSetName"]!);
        
        var path = Path.Combine(Path.GetTempPath(), $"training-set-{Guid.NewGuid():N}.csv");
        try
        {
            await blob.DownloadToAsync(path, ct);

            var ml = new MLContext();
            var data = ml.Data.LoadFromTextFile<TransactionFeatures>(
                path, separatorChar: ',', hasHeader: true, allowQuoting: true, trimWhitespace: true);
            
            return [.. ml.Data.CreateEnumerable<TransactionFeatures>(data, reuseRowObject: false)];
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
        {
            return [];
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Kwota zaokrąglona do groszy, bo z pliku wraca jako <c>float</c>, a z bazy jako
    /// <c>decimal</c> — porównanie bit w bit gubiłoby część duplikatów.
    /// </summary>
    private static (string Description, string Type, decimal Amount) KeyOf(TransactionFeatures f) =>
        (DescriptionNormalizer.Normalize(f.Description),
         f.TransactionType.Trim(),
         Math.Round((decimal)f.Amount, 2));
}
