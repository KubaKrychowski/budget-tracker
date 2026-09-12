using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Reguly kwalifikacji wiersza do zbioru treningowego. To NAJWAZNIEJSZA czesc tego slice'u:
/// zly wiersz w zbiorze nie wywala niczego od razu — psuje model po cichu, a objaw pojawia sie
/// dopiero przy nastepnym imporcie, jako dziwna kategoria z wysoka pewnoscia.
///
/// Testy jada na PRAWDZIWYM Postgresie — patrz uzasadnienie w DashboardHandlerTests.
/// </summary>
public sealed class TrainingSetBuilderTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private string _dataDirectory = null!;
    private string _baseFilePath = null!;

    private int _jedzenieId;
    private int _transportId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();

        var jedzenie = new Category("Jedzenie");
        var transport = new Category("Transport");
        var puste = new Category("Wyposazenie domu");
        _db.Categories.AddRange(jedzenie, transport, puste);
        await _db.SaveChangesAsync();
        _jedzenieId = jedzenie.Id;
        _transportId = transport.Id;

        // Katalog per test — pliki bazowe nie moga sie przenikac miedzy przypadkami.
        _dataDirectory = Path.Combine(Path.GetTempPath(), $"bt-training-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDirectory);
        _baseFilePath = Path.Combine(_dataDirectory, "training-set.csv");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    /// <summary>Plik bazowy w tym samym formacie co produkcyjny: opis, typ, kwota, kategoria.</summary>
    private void WriteBaseFile(params string[] rows) =>
        File.WriteAllLines(_baseFilePath, [
            "\"opis\",\"typ_transakcji\",\"kwota\",\"kategoria\",\"duzy_wydatek\"",
            .. rows,
        ]);

    private TrainingSetBuilder Builder(bool withBaseFile = true) => new(
        _db,
        Options.Create(new CategorizationOptions
        {
            TrainingDataPath = withBaseFile ? _baseFilePath : Path.Combine(_dataDirectory, "nie-ma.csv"),
        }));

    private Transaction New(
        decimal amount, string description, int? categoryId, TransactionStatus status,
        string type = "Obciazenie", DateTimeOffset? createdAt = null) =>
        new(
            new DateOnly(2026, 3, 1),
            amount,
            description,
            createdAt ?? new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            status,
            categoryId: categoryId,
            transactionType: type);

    // ── Reguly kwalifikacji ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Income_never_enters_the_set_however_it_was_categorised()
    {
        // CLAUDE.md §3 (rewizja 2026-09-02): model widzi WYLACZNIE wydatki. Wpuszczenie wplywu
        // przywraca blad, w ktorym wynagrodzenie dostaje "Gastronomie" z pewnoscia ~1.0.
        _db.Transactions.Add(New(5000m, "WYNAGRODZENIE", _jedzenieId, TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        Assert.Equal(0, set.Composition.FromCorrections);
        Assert.Empty(set.Rows);
    }

    [Fact]
    public async Task Auto_categorised_rows_never_enter_the_set()
    {
        // To jest wyjscie modelu. Uczenie na wlasnych predykcjach utrwala bledy i zawyza metryki.
        _db.Transactions.Add(New(-30m, "LIDL", _jedzenieId, TransactionStatus.AutoCategorized));
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        Assert.Empty(set.Rows);
    }

    [Theory]
    [InlineData(TransactionStatus.ManuallyCategorized)]
    [InlineData(TransactionStatus.Confirmed)]
    public async Task Human_decisions_enter_the_set(TransactionStatus status)
    {
        _db.Transactions.Add(New(-30m, "LIDL", _jedzenieId, status));
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        Assert.Equal(1, set.Composition.FromCorrections);
        Assert.Equal("Jedzenie", Assert.Single(set.Rows).Category);
    }

    [Fact]
    public async Task Rows_without_a_category_do_not_enter_the_set()
    {
        // Przyklad bez etykiety nie uczy niczego, a wpuszczony wszedlby z pusta klasa.
        _db.Transactions.Add(New(-30m, "NIEZNANE", null, TransactionStatus.PendingReview));
        _db.Transactions.Add(New(-40m, "TEZ NIEZNANE", null, TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        Assert.Empty(set.Rows);
    }

    [Fact]
    public async Task Deleted_rows_do_not_enter_the_set()
    {
        // Filtr globalny soft delete musi obowiazywac tez tutaj — inaczej model uczylby sie
        // na transakcjach, ktore uzytkownik skasowal, i to bez sladu na ekranie.
        var target = New(-30m, "LIDL", _jedzenieId, TransactionStatus.Confirmed);
        _db.Transactions.Add(target);
        await _db.SaveChangesAsync();

        _db.Transactions.Remove(target);
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        Assert.Empty(set.Rows);
    }

    // ── Normalizacja i deduplikacja ─────────────────────────────────────────────────────

    [Fact]
    public async Task Description_enters_the_set_after_normalisation()
    {
        // Model przy predykcji widzi opis PO normalizacji. Gdyby uczyl sie na surowym,
        // uczylby sie na innym tekscie, niz potem dostaje.
        _db.Transactions.Add(New(
            -30m, "JMP S.A. BIEDRONKA 490 Kraj: POL Data: 2026-03-01", _jedzenieId,
            TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        var row = Assert.Single(set.Rows);
        Assert.Equal(DescriptionNormalizer.Normalize("JMP S.A. BIEDRONKA 490 Kraj: POL Data: 2026-03-01"),
            row.Description);
        Assert.DoesNotContain("2026", row.Description);
        Assert.DoesNotContain("kraj", row.Description);
    }

    [Fact]
    public async Task A_row_present_in_both_the_file_and_the_database_counts_once()
    {
        // Plik bazowy powstal z TYCH SAMYCH realnych transakcji, ktore sa w bazie. Bez odsiania
        // te same przyklady weszlyby dwa razy i przewazyly zbior w strone tego, co juz w nim jest.
        WriteBaseFile("\"lidl 123 miasto warszawa\",\"Obciazenie\",\"-30\",\"Jedzenie\",\"0\"");

        _db.Transactions.Add(New(-30m, "LIDL 123 Miasto Warszawa", _jedzenieId, TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Equal(1, set.Composition.FromFile);
        Assert.Equal(0, set.Composition.FromCorrections);
        Assert.Equal(1, set.Composition.Duplicates);
        Assert.Single(set.Rows);
    }

    [Fact]
    public async Task The_same_merchant_with_a_different_amount_is_a_separate_example()
    {
        // Biedronka na 12 zl i na 300 zl to dwa rozne przyklady — kwota jest cecha modelu,
        // wiec nie moze wypasc z klucza deduplikacji.
        WriteBaseFile("\"lidl\",\"Obciazenie\",\"-30\",\"Jedzenie\",\"0\"");

        _db.Transactions.Add(New(-300m, "LIDL", _jedzenieId, TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Equal(1, set.Composition.FromCorrections);
        Assert.Equal(0, set.Composition.Duplicates);
        Assert.Equal(2, set.Rows.Count);
    }

    [Fact]
    public async Task The_same_description_with_a_different_type_is_a_separate_example()
    {
        // Typ operacji jest cecha rownorzedna z opisem (patrz Transaction.TransactionType):
        // wyplata z bankomatu i zakup w tym samym miejscu maja ten sam opis.
        WriteBaseFile("\"obcy legnicka 29\",\"Obciazenie\",\"-200\",\"Jedzenie\",\"0\"");

        _db.Transactions.Add(New(
            -200m, "OBCY LEGNICKA 29", _transportId, TransactionStatus.Confirmed,
            type: "Wyplata w bankomacie"));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Equal(1, set.Composition.FromCorrections);
        Assert.Equal(2, set.Rows.Count);
    }

    // ── Skladanie zbioru ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_set_is_the_file_plus_corrections()
    {
        // Rdzen zadania: zbior przestaje byc plikiem, a staje sie plikiem PLUS poprawkami.
        WriteBaseFile(
            "\"biedronka\",\"Obciazenie\",\"-10\",\"Jedzenie\",\"0\"",
            "\"orlen\",\"Obciazenie\",\"-250\",\"Transport\",\"0\"");

        _db.Transactions.Add(New(-77m, "ZABKA", _jedzenieId, TransactionStatus.ManuallyCategorized));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Equal(2, set.Composition.FromFile);
        Assert.Equal(1, set.Composition.FromCorrections);
        Assert.Equal(3, set.Composition.Total);
        Assert.Equal(3, set.Rows.Count);
    }

    [Fact]
    public async Task A_missing_base_file_is_not_an_error_the_set_is_then_corrections_only()
    {
        // Swiezy start: pliku bazowego moze nie byc, a poprawki i tak maja czego uczyc.
        _db.Transactions.Add(New(-77m, "ZABKA", _jedzenieId, TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        Assert.Equal(0, set.Composition.FromFile);
        Assert.Equal(1, set.Composition.FromCorrections);
    }

    [Fact]
    public async Task Corrections_are_counted_from_a_point_in_time_for_the_since_last_training_hint()
    {
        var old = New(-10m, "STARA", _jedzenieId, TransactionStatus.Confirmed);
        var fresh = New(-20m, "NOWA", _jedzenieId, TransactionStatus.Confirmed,
            createdAt: new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
        _db.Transactions.AddRange(old, fresh);
        await _db.SaveChangesAsync();

        // ⚠️ Daty przychodza RAZEM ze zbiorem, nie osobnym zapytaniem. Wczesniej handler
        // pytal baze drugi raz o ten sam predykat tylko po to, zeby je policzyc.
        var set = await Builder(withBaseFile: false).BuildAsync(default);

        Assert.Equal(2, set.CorrectionDates.Count);
        Assert.Equal(1, set.CorrectionsNewerThan(
            new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)));

        // Rosnaco — na tej kolejnosci opiera sie regula „nowsza poprawka wygrywa".
        Assert.Equal(set.CorrectionDates.OrderBy(d => d), set.CorrectionDates);
    }

    // ── Poprawka kontra plik bazowy ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_correction_that_contradicts_the_file_wins_over_it()
    {
        // ⚠️ REGRESJA. Poprawka wiersza, ktory JUZ JEST w pliku, byla wyrzucana razem
        // z duplikatami — wiec model dalej uczyl sie starej, zlej etykiety z pliku.
        // To dokladnie te poprawki sa najcenniejsze: powstaja tam, gdzie plik sie myli.
        WriteBaseFile("\"castorama\",\"Obciazenie\",\"-120\",\"Jedzenie\",\"0\"");

        _db.Transactions.Add(New(-120m, "CASTORAMA", _transportId, TransactionStatus.ManuallyCategorized));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        // Zbior NIE rosnie — to ten sam przyklad, tylko z inna etykieta.
        Assert.Single(set.Rows);
        Assert.Equal(1, set.Composition.FromFile);
        Assert.Equal(0, set.Composition.FromCorrections);
        Assert.Equal(1, set.Composition.Corrected);
        Assert.Equal(0, set.Composition.Duplicates);

        Assert.Equal("Transport", set.Rows[0].Category);
    }

    [Fact]
    public async Task A_correction_identical_to_the_file_is_still_a_duplicate()
    {
        // Odsiewanie zostaje tam, gdzie mialo sens: ten sam przyklad z TA SAMA etykieta
        // wszedlby do zbioru dwa razy i przewazylby go.
        WriteBaseFile("\"lidl\",\"Obciazenie\",\"-30\",\"Jedzenie\",\"0\"");

        _db.Transactions.Add(New(-30m, "LIDL", _jedzenieId, TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Single(set.Rows);
        Assert.Equal(1, set.Composition.Duplicates);
        Assert.Equal(0, set.Composition.Corrected);
    }

    [Fact]
    public async Task A_correction_relabels_every_occurrence_of_the_same_example()
    {
        // Wiersze o IDENTYCZNYCH cechach i sprzecznych etykietach to dla modelu szum,
        // a nie dwie opinie — wiec decyzja czlowieka przestawia wszystkie wystapienia.
        WriteBaseFile(
            "\"zabka\",\"Obciazenie\",\"-12\",\"Jedzenie\",\"0\"",
            "\"zabka\",\"Obciazenie\",\"-12\",\"Jedzenie\",\"0\"",
            "\"zabka\",\"Obciazenie\",\"-12\",\"Jedzenie\",\"0\"");

        _db.Transactions.Add(New(-12m, "ZABKA", _transportId, TransactionStatus.ManuallyCategorized));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Equal(3, set.Rows.Count);
        Assert.All(set.Rows, r => Assert.Equal("Transport", r.Category));
        Assert.Equal(1, set.Composition.Corrected);
    }

    [Fact]
    public async Task When_two_corrections_disagree_the_newer_one_wins()
    {
        // Bez jawnej kolejnosci wynik zalezalby od tego, co zwroci baza. Nowsza decyzja
        // czlowieka jest najlepsza dostepna prawda.
        WriteBaseFile("\"castorama\",\"Obciazenie\",\"-120\",\"Jedzenie\",\"0\"");

        var starsza = New(-120m, "CASTORAMA", _transportId, TransactionStatus.ManuallyCategorized,
            createdAt: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

        var nowsza = New(-120m, "CASTORAMA", _jedzenieId, TransactionStatus.ManuallyCategorized,
            createdAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        _db.Transactions.AddRange(starsza, nowsza);
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Equal("Jedzenie", Assert.Single(set.Rows).Category);
    }

    [Fact]
    public async Task A_correction_of_a_row_that_is_not_in_the_file_still_just_adds_it()
    {
        // Sciezka „nowy przyklad" nie moze ucierpiec na zmianie regul dla sprzecznych etykiet.
        WriteBaseFile("\"lidl\",\"Obciazenie\",\"-30\",\"Jedzenie\",\"0\"");

        _db.Transactions.Add(New(-77m, "ZABKA", _transportId, TransactionStatus.Confirmed));
        await _db.SaveChangesAsync();

        var set = await Builder().BuildAsync(default);

        Assert.Equal(2, set.Rows.Count);
        Assert.Equal(1, set.Composition.FromCorrections);
        Assert.Equal(0, set.Composition.Corrected);
    }

    [Fact]
    public async Task Two_conflicting_corrections_outside_the_file_count_as_ONE_new_example()
    {
        // ⚠️ Regresja liczenia. Pierwsza poprawka trafia do `added` (FromCorrections=1).
        // Druga, o tym samym kluczu i innej kategorii, NADPISUJE ten sam obiekt — wiec
        // wczesniej doliczala sie takze do `Corrected`, i ekran pokazywal „1 nowy
        // + 1 przeetykietowany" tam, gdzie powstal JEDEN przyklad.
        //
        // Realny scenariusz: cykliczna oplata na te sama kwote, poprawiona dwa razy.
        var starsza = New(-49.99m, "SUBSKRYPCJA X", _jedzenieId, TransactionStatus.ManuallyCategorized,
            createdAt: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

        var nowsza = New(-49.99m, "SUBSKRYPCJA X", _transportId, TransactionStatus.ManuallyCategorized,
            createdAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        _db.Transactions.AddRange(starsza, nowsza);
        await _db.SaveChangesAsync();

        var set = await Builder(withBaseFile: false).BuildAsync(default);

        // Jeden przyklad, z nowsza etykieta.
        Assert.Equal("Transport", Assert.Single(set.Rows).Category);
        Assert.Equal(1, set.Composition.FromCorrections);

        // `Corrected` opisuje WYLACZNIE wiersze pliku bazowego — a tego wiersza w pliku nie ma.
        Assert.Equal(0, set.Composition.Corrected);
    }
}
