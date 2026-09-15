using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Import.Commands;
using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Models;
using BudgetTracker.Api.Features.Import.Queries;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Testy orkiestracji importu. Parser jest tu nieistotny — handler dostaje gotowe
/// <see cref="ParsedRow"/>, więc te testy działają BEZ pliku wyciągu.
///
/// Import jest dwufazowy: podgląd liczy, zapis utrwala to, co użytkownik zaakceptował.
/// Testy idą tą samą drogą co stepper — najpierw <c>PreviewAsync</c>, potem <c>CommitAsync</c>
/// na wierszach z podglądu — bo tylko to sprawdza przepływ, którego naprawdę używa aplikacja.
///
/// Kategoryzator podmieniony na atrapę o sterowalnej pewności, żeby dało się sprawdzić
/// zachowanie wokół progu bez zależności od reguł ani modelu.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class ImportFeatureTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_import_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private Guid _budgetId;
    private Guid _categoryId;
    private Guid _otherCategoryId;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero));

    /// <summary>Atrapa: zwraca ustaloną kategorię z ustaloną pewnością, albo nic.</summary>
    private sealed class StubCategorizer(int? categoryId, decimal? confidence) : ICategorizer
    {
        public Task<CategorySuggestion> CategorizeAsync(string d, string t, decimal a, CancellationToken ct)
            => Task.FromResult(new CategorySuggestion(categoryId, confidence));
    }

    public async Task InitializeAsync()
    {
        // Interceptor tez w tescie — tak samo jak w produkcji. Odpowiada wylacznie za zamiane
        // fizycznego kasowania na logiczne; BusinessId nadaje sobie sama encja.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        var category = new Category("Jedzenie");
        var other = new Category("Gastronomia");
        var budget = new Budget("Podstawowy", new DateOnly(2026, 8, 1), 0m, default);
        _db.Categories.AddRange(category, other);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        _categoryId = category.BusinessId;
        _otherCategoryId = other.BusinessId;
        _budgetId = budget.BusinessId;
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    /// <summary>Oba handlery importu na wspólnych serwisach — podgląd i zapis idą tą samą drogą co w aplikacji.</summary>
    private sealed record ImportHandlers(GetImportPreviewQueryHandler Preview, CommitImportCommandHandler Commit);

    private ImportHandlers Handler(Guid? categoryId = null, decimal? confidence = null, decimal threshold = 0.7m)
    {
        var budgets = new ImportBudgetLookup(_db);
        var existingKeys = new ExistingTransactionKeys(_db);
        var confidenceThreshold = new ImportConfidenceThreshold(
            Options.Create(new CategorizationOptions { ConfidenceThreshold = threshold }));

        return new ImportHandlers(
            new GetImportPreviewQueryHandler(_db, new StubCategorizer(KeyOf(categoryId), confidence),
                // Prawdziwy ModelStore, nie atrapa: import bierze z niego wylacznie znacznik
                // "trwa kategoryzacja", ktory nie dotyka dysku.
                new ModelStore(Options.Create(new CategorizationOptions()), _clock),
                budgets, existingKeys, confidenceThreshold),
            new CommitImportCommandHandler(
                _db, _clock, budgets, existingKeys, confidenceThreshold,
                new StandingOrderMatcher(_db), new SavingsTransferMatcher(_db)));
    }

    /// <summary>
    /// Publiczny identyfikator → klucz z bazy. Atrapa kategoryzatora zwraca KLUCZ, bo
    /// <c>ICategorizer</c> pracuje po stronie bazy — Guidy są tylko na granicy API.
    /// </summary>
    private int? KeyOf(Guid? businessId) => businessId is { } id
        ? _db.Categories.Single(c => c.BusinessId == id).Id
        : null;

    private static ParsedRow Row(string desc, decimal amount, int day = 5, string? reference = null)
        => new(new DateOnly(2026, 8, day), amount, desc, "Obciążenie", reference);

    /// <summary>
    /// Przejście całą drogą steppera: podgląd → zatwierdzenie wszystkiego, co podgląd
    /// pokazał. Duplikatów front nie odsyła — oznacza je i pomija, tak jak tabela w kroku 3.
    /// </summary>
    private async Task<ImportSummaryResponseDto> ImportAll(
        ImportHandlers handler, IReadOnlyList<ParsedRow> rows, string fileName = "wyciag.csv")
    {
        var preview = await handler.Preview.HandleAsync(rows, _budgetId, default);
        return await handler.Commit.HandleAsync(
            new CommitRequestDto(_budgetId, "pko", fileName, RowsToKeep(preview)), default);
    }

    private static List<CommitRowRequestDto> RowsToKeep(ImportPreviewResponseDto preview) => preview.Rows
        .Where(r => !r.Duplicate)
        .Select(r => new CommitRowRequestDto(
            r.Date, r.Amount, r.Description, r.TransactionType, r.ExternalReference,
            r.CategoryId, r.Confidence, Edited: false))
        .ToList();

    // ── Scalanie i deduplikacja ────────────────────────────────────────────────────────

    [Fact]
    public async Task Merges_identical_rows_into_one_summed_transaction()
    {
        // Trzy bilety po 4,20 zl tego samego dnia — nierozroznialne zadnym kluczem.
        var rows = new[] { Row("BILET IKO", -4.20m), Row("BILET IKO", -4.20m), Row("BILET IKO", -4.20m) };

        var preview = await Handler().Preview.HandleAsync(rows, _budgetId, default);

        Assert.Equal(1, preview.RowsInFile);
        Assert.Equal(-12.60m, Assert.Single(preview.Rows).Amount);
    }

    [Fact]
    public async Task Merging_makes_reimport_of_the_same_file_a_no_op()
    {
        // To jest sedno scalania: gdyby zapisywalo trzy osobne pozycje, drugi import
        // porownywalby wiersze po 4,20 z rekordami po 4,20 i tez by je pominal — ale
        // import DLUZSZEGO wyciagu juz nie. Scalanie musi dawac ten sam wynik za kazdym razem.
        var rows = new[] { Row("BILET IKO", -4.20m), Row("BILET IKO", -4.20m), Row("BILET IKO", -4.20m) };

        await ImportAll(Handler(), rows);
        var second = await Handler().Preview.HandleAsync(rows, _budgetId, default);

        Assert.Equal(1, second.SkippedDuplicates);
        Assert.Equal(0, second.WillImport);
        Assert.Equal(1, await _db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Rows_with_distinct_reference_numbers_are_not_merged()
    {
        // Ta sama kwota i data, ale rozne referencje = rozne transakcje.
        var rows = new[]
        {
            Row("SKLEP", -20m, reference: "REF-1"),
            Row("SKLEP", -20m, reference: "REF-2"),
        };

        var summary = await ImportAll(Handler(), rows);

        Assert.Equal(2, summary.RowsInFile);
        Assert.Equal(2, await _db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Longer_statement_containing_previous_one_imports_only_the_new_rows()
    {
        await ImportAll(Handler(), [Row("SKLEP A", -10m, day: 1)], "krotki.csv");

        var preview = await Handler().Preview.HandleAsync(
            [Row("SKLEP A", -10m, day: 1), Row("SKLEP B", -20m, day: 2)], _budgetId, default);

        Assert.Equal(1, preview.SkippedDuplicates);
        await Handler().Commit.HandleAsync(
            new CommitRequestDto(_budgetId, "pko", "dlugi.csv", RowsToKeep(preview)), default);

        Assert.Equal(2, await _db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Commit_rechecks_duplicates_because_time_passes_between_the_two_steps()
    {
        // Uzytkownik moze zostawic otwarty podglad dowolnie dlugo. Gdyby zapis ufal
        // wynikowi podgladu, import wykonany w miedzyczasie zostalby zdublowany.
        var rows = new[] { Row("SKLEP", -10m, reference: "R1") };
        var preview = await Handler().Preview.HandleAsync(rows, _budgetId, default);

        await ImportAll(Handler(), rows, "inny.csv");   // ktos zdazyl zaimportowac to samo

        var summary = await Handler().Commit.HandleAsync(
            new CommitRequestDto(_budgetId, "pko", "wyciag.csv", RowsToKeep(preview)), default);

        Assert.Equal(1, summary.SkippedDuplicates);
        Assert.Equal(0, summary.Imported);
        Assert.Equal(1, await _db.Transactions.CountAsync());
    }

    [Fact]
    public async Task The_same_statement_can_be_imported_into_a_second_budget()
    {
        // SEDNO wielu budzetow: sluza do porownywania wariantow TYCH SAMYCH danych.
        // Przy globalnej deduplikacji drugi wariant przyszedlby pusty — kazdy wiersz
        // bylby „duplikatem" wiersza z pierwszego budzetu.
        var rows = new[] { Row("SKLEP", -50m, reference: "R1"), Row("STACJA", -200m, reference: "R2") };
        await ImportAll(Handler(_categoryId, 0.9m), rows);

        var second = new Budget("Wariant B", new DateOnly(2026, 8, 1), 0m, default);
        _db.Budgets.Add(second);
        await _db.SaveChangesAsync();

        var handler = Handler(_categoryId, 0.9m);
        var preview = await handler.Preview.HandleAsync(rows, second.BusinessId, default);

        Assert.Equal(0, preview.SkippedDuplicates);
        Assert.Equal(2, preview.WillImport);

        var summary = await handler.Commit.HandleAsync(
            new CommitRequestDto(second.BusinessId, "pko", "wyciag.csv", RowsToKeep(preview)), default);

        Assert.Equal(2, summary.Imported);
        // Kazdy budzet ma wlasny komplet — 2 + 2, nie 2.
        Assert.Equal(2, await _db.Transactions.CountAsync(t => t.BudgetBusinessId == _budgetId));
        Assert.Equal(2, await _db.Transactions.CountAsync(t => t.BudgetBusinessId == second.BusinessId));
    }

    [Fact]
    public async Task Duplicates_are_still_caught_within_the_same_budget()
    {
        // Zawezenie do budzetu nie moze rozbroic deduplikacji tam, gdzie ma dzialac.
        var rows = new[] { Row("SKLEP", -50m, reference: "R1") };
        await ImportAll(Handler(_categoryId, 0.9m), rows);

        var preview = await Handler(_categoryId, 0.9m).Preview.HandleAsync(rows, _budgetId, default);

        Assert.Equal(1, preview.SkippedDuplicates);
        Assert.Equal(0, preview.WillImport);
    }

    // ── Prog pewnosci ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.95, TransactionStatus.AutoCategorized)]
    [InlineData(0.70, TransactionStatus.AutoCategorized)]   // prog wlaczajaco
    [InlineData(0.69, TransactionStatus.PendingReview)]
    [InlineData(0.10, TransactionStatus.PendingReview)]
    public async Task Confidence_threshold_decides_the_status(decimal confidence, TransactionStatus expected)
    {
        await ImportAll(Handler(_categoryId, confidence), [Row("SKLEP", -30m)]);

        var saved = await _db.Transactions.SingleAsync();
        Assert.Equal(expected, saved.Status);
    }

    [Fact]
    public async Task Uncertain_suggestion_is_shown__but_never_as_a_settled_fact()
    {
        // ⚠️ ODWROCONA REGULA (na zyczenie uzytkownika). Wczesniej niepewna podpowiedz byla
        // wyrzucana, zeby nikt nie uznal jej za ustalona — ale kosztem bylo puste pole i wybor
        // z 25 kategorii przy KAZDYM takim wierszu, mimo ze model mial zdanie.
        //
        // Dzis podpowiedz zostaje, a przed uznaniem jej za fakt chroni `NeedsReview`: wiersz
        // dalej stoi w kolejce, dalej liczy sie jako "do weryfikacji" i dalej zapisze sie jako
        // `PendingReview`, dopoki czlowiek go nie przyjmie. Widoczna podpowiedz to propozycja,
        // nie rozstrzygniecie — i tego pilnuja testy nizej.
        var preview = await Handler(_categoryId, 0.4m).Preview.HandleAsync([Row("SKLEP", -30m)], _budgetId, default);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(_categoryId, row.CategoryId);
        Assert.Equal(0.4m, row.Confidence);
        Assert.True(row.NeedsReview);
    }

    [Fact]
    public async Task Threshold_is_configurable()
    {
        // CLAUDE.md §9 nazywa 0.7 zalozeniem do strojenia — nie moze byc zaszyte w kodzie.
        await ImportAll(Handler(_categoryId, 0.5m, threshold: 0.4m), [Row("SKLEP", -30m)]);

        var saved = await _db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.AutoCategorized, saved.Status);
    }

    // ── Edycja w kroku 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rows_the_user_removed_are_not_saved()
    {
        var preview = await Handler(_categoryId, 0.95m)
            .Preview.HandleAsync([Row("ZOSTAJE", -10m, reference: "R1"), Row("USUNIETY", -20m, reference: "R2")], _budgetId, default);

        // Front odsyla tylko to, co zostalo w tabeli po usunieciu zaznaczonych wierszy.
        var kept = RowsToKeep(preview).Where(r => r.Description == "ZOSTAJE").ToList();
        var summary = await Handler().Commit.HandleAsync(
            new CommitRequestDto(_budgetId, "pko", "wyciag.csv", kept), default);

        Assert.Equal(1, summary.Imported);
        Assert.Equal("ZOSTAJE", (await _db.Transactions.SingleAsync()).Description);
    }

    [Fact]
    public async Task Category_corrected_by_the_user_is_saved_as_manual_not_automatic()
    {
        // Sedno: poprawka czlowieka nie moze wrocic do kolejki „do przegladu",
        // ktora wlasnie recznie rozbroil.
        var preview = await Handler(_categoryId, 0.3m).Preview.HandleAsync([Row("COS NIEJASNEGO", -30m)], _budgetId, default);
        var row = Assert.Single(preview.Rows);

        await Handler().Commit.HandleAsync(new CommitRequestDto(_budgetId, "pko", "w.csv", [
            new CommitRowRequestDto(row.Date, row.Amount, row.Description, row.TransactionType,
                row.ExternalReference, _otherCategoryId, row.Confidence, Edited: true),
        ]), default);

        var saved = await _db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.ManuallyCategorized, saved.Status);
        Assert.Equal(_otherCategoryId, (await _db.Categories.SingleAsync(c => c.Id == saved.CategoryId)).BusinessId);
        // Kategoria od czlowieka nie jest predykcja, wiec nie ma „pewnosci".
        Assert.Null(saved.Confidence);
    }

    // ── Podsumowanie (krok 4) ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_reports_the_numbers_shown_on_the_final_screen()
    {
        var summary = await ImportAll(Handler(_categoryId, 0.9m), [
            Row("WYDATEK", -100m, day: 3, reference: "R1"),
            Row("WPLYW", 250m, day: 20, reference: "R2"),
        ]);

        Assert.Equal(100m, summary.TotalExpenses);      // dodatnia — ekran pokazuje „sume wydatkow"
        Assert.Equal(250m, summary.TotalIncome);
        Assert.Equal(new DateOnly(2026, 8, 3), summary.PeriodFrom);
        Assert.Equal(new DateOnly(2026, 8, 20), summary.PeriodTo);
        Assert.Equal(0.9m, summary.AverageConfidence);
    }

    [Fact]
    public async Task Budget_balance_is_the_opening_balance_moved_by_the_imported_rows()
    {
        // Ta sama formula co na dashboardzie: bilans poczatkowy + wszystkie transakcje
        // budzetu. Wczesniej bylo „suma limitow minus wydatki", wiec ekran podsumowania
        // importu i dashboard pokazywaly dla tego samego budzetu dwie rozne kwoty.
        var budget = await _db.Budgets.SingleAsync(b => b.BusinessId == _budgetId);
        budget.Update(budget.Name, 500m);
        await _db.SaveChangesAsync();

        var summary = await ImportAll(Handler(_categoryId, 0.9m), [
            Row("SKLEP", -120m, reference: "R1"),
            Row("ZWROT", 20m, reference: "R2"),
        ]);

        // Wplyw podnosi bilans, wydatek go obniza: 500 - 120 + 20 = 400.
        Assert.Equal(400m, summary.BudgetBalance);
    }

    [Fact]
    public async Task Batch_records_counters_and_links_transactions()
    {
        var summary = await ImportAll(Handler(_categoryId, 0.9m),
            [Row("A", -10m, reference: "R1"), Row("B", -20m, reference: "R2")]);

        var batch = await _db.ImportBatches.SingleAsync();
        var budget = await _db.Budgets.SingleAsync(b => b.Id == batch.BudgetId);
        Assert.Equal("wyciag.csv", batch.FileName);
        Assert.Equal("pko", batch.Bank);
        Assert.Equal(_budgetId, budget.BusinessId);
        Assert.Equal(2, batch.ImportedCount);
        Assert.Equal(summary.BatchId, batch.BusinessId);

        var saved = await _db.Transactions.ToListAsync();
        Assert.All(saved, t => Assert.Equal(batch.Id, t.ImportBatchId));
        // Budzet wybrany w kroku 1 ladzie na kazdej transakcji, nie tylko na batchu.
        Assert.All(saved, t => Assert.Equal(_budgetId, t.BudgetBusinessId));
    }

    [Fact]
    public async Task Import_time_comes_from_the_clock_not_from_the_machine()
    {
        await ImportAll(Handler(), [Row("SKLEP", -10m)]);

        var batch = await _db.ImportBatches.SingleAsync();
        Assert.Equal(_clock.GetUtcNow(), batch.ImportedAt);
    }

    [Fact]
    public async Task Preview_does_not_write_anything()
    {
        var preview = await Handler(_categoryId, 0.95m)
            .Preview.HandleAsync([Row("BIEDRONKA", -50m), Row("ZABKA", -12m)], _budgetId, default);

        Assert.Equal(2, preview.WillImport);
        // Sedno kroku „Podglad": uzytkownik moze sie wycofac i baza ma o tym nie wiedziec.
        Assert.Empty(await _db.Transactions.ToListAsync());
        Assert.Empty(await _db.ImportBatches.ToListAsync());
    }

    [Fact]
    public async Task Preview_marks_rows_already_present_in_the_database()
    {
        var rows = new[] { Row("BIEDRONKA", -50m, reference: "R1") };
        await ImportAll(Handler(_categoryId, 0.95m), rows);

        var preview = await Handler(_categoryId, 0.95m).Preview.HandleAsync(rows, _budgetId, default);

        var row = Assert.Single(preview.Rows);
        Assert.True(row.Duplicate);
        // Duplikat nie dostaje kategorii — nie bedzie zapisany, wiec nie ma czego proponowac.
        Assert.Null(row.CategoryId);
        Assert.Equal(1, preview.SkippedDuplicates);
        Assert.Equal(0, preview.WillImport);
    }

    // ── Podpowiedz ponizej progu ────────────────────────────────────────────────────────

    [Fact]
    public async Task Niepewna_podpowiedz_ZOSTAJE_w_wierszu__ale_wiersz_czeka_na_weryfikacje()
    {
        // Wczesniej predykcja ponizej progu byla wyrzucana i wiersz przychodzil pusty, wiec
        // uzytkownik wybieral z 25 kategorii od zera — mimo ze model mial zdanie, tylko niepewne.
        var handler = Handler(_categoryId, confidence: 0.46m);

        var preview = await handler.Preview.HandleAsync([Row("NIEZNANY SKLEP", -20m, day: 3)], _budgetId, default);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(_categoryId, row.CategoryId);
        Assert.Equal(0.46m, row.Confidence);
        Assert.True(row.NeedsReview);
    }

    [Fact]
    public async Task Liczniki_podgladu_ida_po_NeedsReview__nie_po_obecnosci_kategorii()
    {
        // ⚠️ Po staremu ten sam wiersz wpadlby do "do zapisania", bo MA kategorie —
        // ekran pokazalby "do weryfikacji: 0" nad wierszem wolajacym o przeglad.
        var handler = Handler(_categoryId, confidence: 0.46m);

        var preview = await handler.Preview.HandleAsync([Row("NIEZNANY SKLEP", -20m, day: 3)], _budgetId, default);

        Assert.Equal(0, preview.WillImport);
        Assert.Equal(1, preview.PendingReview);
    }

    [Fact]
    public async Task Pewna_podpowiedz_dalej_wchodzi_bez_przegladu()
    {
        // Regula ma pewnosc 1.0, model powyzej progu tak samo — bramka nie moze ich zdusic.
        var handler = Handler(_categoryId, confidence: 0.95m);

        var preview = await handler.Preview.HandleAsync([Row("BIEDRONKA", -20m, day: 3)], _budgetId, default);

        var row = Assert.Single(preview.Rows);
        Assert.False(row.NeedsReview);
        Assert.Equal(1, preview.WillImport);
    }

    [Fact]
    public async Task Niepewna_kategoria_zapisuje_sie_jako_DO_PRZEGLADU__nie_jako_automatyczna()
    {
        // NAJWAZNIEJSZY test tej zmiany. Wiersz niesie teraz kategorie takze wtedy, gdy model
        // nie byl jej pewny — gdyby zapis patrzyl tylko na "czy jest kategoria", niepewna
        // predykcja utrwalilaby sie jako AutoCategorized, czyli jako cos, czego nikt nie
        // sprawdzil i juz nie sprawdzi.
        var handler = Handler(_categoryId, confidence: 0.46m);

        await ImportAll(handler, [Row("NIEZNANY SKLEP", -20m, day: 3)]);

        var saved = Assert.Single(await _db.Transactions.ToListAsync());
        Assert.Equal(TransactionStatus.PendingReview, saved.Status);
        Assert.NotNull(saved.CategoryId);
    }

    [Fact]
    public async Task Akceptacja_podpowiedzi_zapisuje_sie_jako_decyzja_czlowieka()
    {
        // Front oznacza przyjeta podpowiedz jako `Edited` — i to musi wystarczyc, zeby wiersz
        // wyszedl z kolejki. Inaczej "Akceptuj" nie zmienialoby niczego poza wygladem.
        var handler = Handler(_categoryId, confidence: 0.46m);
        var preview = await handler.Preview.HandleAsync([Row("NIEZNANY SKLEP", -20m, day: 3)], _budgetId, default);

        var accepted = preview.Rows
            .Select(r => new CommitRowRequestDto(
                r.Date, r.Amount, r.Description, r.TransactionType, r.ExternalReference,
                r.CategoryId, r.Confidence, Edited: true))
            .ToList();

        await handler.Commit.HandleAsync(new CommitRequestDto(_budgetId, "pko", "wyciag.csv", accepted), default);

        var saved = Assert.Single(await _db.Transactions.ToListAsync());
        Assert.Equal(ManualCategoryCorrection.Status, saved.Status);
        Assert.NotNull(saved.CategoryId);

        // Decyzja czlowieka nie jest predykcja — pewnosc znika razem z autorstwem modelu.
        Assert.Null(saved.Confidence);
    }

    [Fact]
    public async Task Brak_podpowiedzi_dalej_zostawia_wiersz_pusty()
    {
        // Bramka slownikowa (#9) mowi "model nie zna zadnego slowa z tego opisu" — to co
        // innego niz "model jest niepewny". Nie ma czego podstawiac.
        var handler = Handler(categoryId: null, confidence: null);

        var preview = await handler.Preview.HandleAsync([Row("ZZZZ WWWW", -20m, day: 3)], _budgetId, default);

        var row = Assert.Single(preview.Rows);
        Assert.Null(row.CategoryId);
        Assert.True(row.NeedsReview);
    }

}
