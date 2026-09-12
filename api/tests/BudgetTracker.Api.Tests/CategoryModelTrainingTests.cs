using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Ekran „Dane treningowe": co pokazuje i czego odmawia. Trening jako taki (uczenie ML.NET)
/// nie jest tu odpalany — jest wolny i sprawdza go <c>/verify</c> recznie; testowalne
/// i warte testu sa REGULY wokol niego.
/// </summary>
public sealed class CategoryModelTrainingTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private string _directory = null!;
    private FakeTimeProvider _clock = null!;
    private ModelStore _store = null!;
    private IOptions<CategorizationOptions> _categorization = null!;
    private StubCategorizer _categorizer = null!;

    private int _jedzenieId;
    private int _wynagrodzenieId;

    /// <summary>Odpowiedzi atrapy kategoryzatora — ustawiane per test.</summary>
    private readonly Dictionary<string, CategorySuggestion> _answers = [];

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
        var wynagrodzenie = new Category("Wynagrodzenie");
        _db.Categories.AddRange(jedzenie, wynagrodzenie);
        await _db.SaveChangesAsync();
        _jedzenieId = jedzenie.Id;
        _wynagrodzenieId = wynagrodzenie.Id;

        _directory = Path.Combine(Path.GetTempPath(), $"bt-handler-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        var categorization = Options.Create(new CategorizationOptions
        {
            ModelPath = Path.Combine(_directory, "category-model.zip"),
            TrainingDataPath = Path.Combine(_directory, "training-set.csv"),
        });

        _clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero));
        _store = new ModelStore(categorization, _clock);
        _categorizer = new StubCategorizer(_answers);
        _categorization = categorization;
    }

    private GetTrainingSetQueryHandler OverviewHandler() =>
        new(_db, new TrainingSetBuilder(_db, _categorization), _store);

    private TrainCategoryModelCommandHandler TrainHandler() =>
        new(new TrainingSetBuilder(_db, _categorization), _store);

    private RecategorizeTransactionsCommandHandler RecategorizeHandler() =>
        new(_db, _store, _categorizer, _categorization);

    private ActivateCategoryModelCommandHandler ActivateHandler() => new(_store);

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    /// <summary>
    /// Atrapa kategoryzatora: odpowiada według mapy „znormalizowany opis → sugestia".
    /// Opis spoza mapy dostaje brak — czyli to samo, co model, który nie zna ani jednego słowa.
    ///
    /// Mapa jest współdzielona z testem PRZEZ REFERENCJĘ, żeby każdy test mógł ustawić własne
    /// odpowiedzi bez budowania handlera od nowa.
    /// </summary>
    private sealed class StubCategorizer(Dictionary<string, CategorySuggestion> answers) : ICategorizer
    {
        /// <summary>Haczyk odpalany przy każdym wywołaniu — pozwala zajrzeć w ŚRODEK przebiegu.</summary>
        public Action? OnCall { get; set; }

        public Task<CategorySuggestion> CategorizeAsync(string d, string t, decimal a, CancellationToken ct)
        {
            OnCall?.Invoke();
            return Task.FromResult(answers.TryGetValue(d, out var suggestion) ? suggestion : CategorySuggestion.None);
        }
    }

    private Transaction AddTransaction(
        string description, TransactionStatus status, int? categoryId, decimal? confidence = null)
    {
        var transaction = new Transaction(
                              new DateOnly(2026, 3, 1),
                              -20m,
                              description,
                              new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                              status,
                              categoryId: categoryId,
                              confidence: confidence,
                              transactionType: "Obciazenie");
        _db.Transactions.Add(transaction);
        return transaction;
    }

    private void AddCorrection(decimal amount, string description, int? categoryId = null) =>
        _db.Transactions.Add(new Transaction(
                                 new DateOnly(2026, 3, 1),
                                 amount,
                                 description,
                                 new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                                 TransactionStatus.Confirmed,
                                 categoryId: categoryId ?? _jedzenieId,
                                 transactionType: "Obciazenie"));

    [Fact]
    public async Task Overview_lists_categories_with_zero_examples_as_a_first_class_state()
    {
        // ⚠️ Rozklad idzie z BAZY, nie ze zbioru: kategoria, ktorej model nigdy nie widzial,
        // nie pojawilaby sie w zbiorze wcale — a to wlasnie ona jest tu warta pokazania,
        // bo model nigdy jej nie wskaze. Na realnych danych sa to kategorie przychodowe.
        AddCorrection(-30m, "LIDL");
        await _db.SaveChangesAsync();

        var overview = await OverviewHandler().HandleAsync(default);

        Assert.Equal(1, overview.Categories.Single(c => c.Name == "Jedzenie").Count);
        Assert.Equal(0, overview.Categories.Single(c => c.Name == "Wynagrodzenie").Count);
    }

    [Fact]
    public async Task Overview_counts_the_review_queue_separately_from_the_training_set()
    {
        // Kolejka przegladu to PALIWO, ktorego jeszcze nie zebrano — nie wchodzi do zbioru,
        // ale ma zachecac do przejscia na liste transakcji.
        _db.Transactions.Add(new Transaction(
                                 new DateOnly(2026, 3, 2),
                                 -12m,
                                 "NIEZNANE",
                                 new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero),
                                 TransactionStatus.PendingReview,
                                 transactionType: "Obciazenie"));
        AddCorrection(-30m, "LIDL");
        await _db.SaveChangesAsync();

        var overview = await OverviewHandler().HandleAsync(default);

        Assert.Equal(1, overview.PendingReview);
        Assert.Equal(1, overview.Composition.FromCorrections);
    }

    [Fact]
    public async Task Without_training_history_new_corrections_are_counted_against_the_base_file()
    {
        // Punktem odniesienia jest plik bazowy — to na nim uczyl sie model, ktory dziala dzis.
        // Liczenie WSZYSTKICH kwalifikujacych sie obiecywaloby material, ktory model juz widzial.
        File.WriteAllLines(
            Path.Combine(_directory, "training-set.csv"),
            [
                "\"opis\",\"typ_transakcji\",\"kwota\",\"kategoria\",\"duzy_wydatek\"",
                "\"lidl\",\"Obciazenie\",\"-30\",\"Jedzenie\",\"0\"",
            ]);

        AddCorrection(-30m, "LIDL");   // juz w pliku — model to widzial
        AddCorrection(-77m, "ZABKA");  // nowe
        await _db.SaveChangesAsync();

        var overview = await OverviewHandler().HandleAsync(default);

        Assert.Equal(1, overview.Composition.Duplicates);
        Assert.Equal(1, overview.CorrectionsSinceLastTraining);
    }

    [Fact]
    public async Task Training_is_refused_with_409_while_an_import_is_categorising()
    {
        AddCorrection(-30m, "LIDL");
        await _db.SaveChangesAsync();

        using var import = _store.BeginImport();

        await Assert.ThrowsAsync<TrainingBusyException>(() => TrainHandler().HandleAsync(default));
    }

    [Fact]
    public async Task Training_with_nothing_to_learn_from_is_a_bad_request_not_a_crash()
    {
        // Ani pliku bazowego, ani kwalifikujacych sie poprawek — to stan konfiguracji,
        // nie awaria, wiec 400. Bez tego ML.NET wywalilby sie na pustym zbiorze (500).
        await Assert.ThrowsAsync<TrainingDataMissingException>(() => TrainHandler().HandleAsync(default));
    }

    [Fact]
    public async Task A_failed_training_does_not_hold_the_lock_forever()
    {
        // Blokada zwalniana przez `using`, takze gdy trening rzuci — inaczej pierwszy nieudany
        // trening zablokowalby wszystkie nastepne az do restartu procesu.
        await Assert.ThrowsAsync<TrainingDataMissingException>(() => TrainHandler().HandleAsync(default));

        using var lease = _store.TryBeginTraining();
        Assert.NotNull(lease);
    }

    [Fact]
    public void Restoring_an_unknown_version_reports_not_found()
    {
        Assert.Throws<ModelVersionNotFoundException>(() => ActivateHandler().Handle("20990101000000"));
    }

    [Fact]
    public void Restoring_is_refused_while_an_import_is_categorising()
    {
        // Podmiana modelu w trakcie importu jest tak samo grozna jak trening: polowa wyciagu
        // dostalaby kategorie z jednego modelu, polowa z drugiego.
        using var import = _store.BeginImport();

        Assert.Throws<TrainingBusyException>(() => ActivateHandler().Handle("20260906100000"));
    }

    // ── Przeliczanie kategorii wierszy, które są już w bazie ─────────────────────────────

    /// <summary>
    /// NAJWAŻNIEJSZY test tej operacji. Wiersze, o których zdecydował człowiek, są materiałem
    /// treningowym modelu — przepisanie ich predykcją tego samego modelu zamknęłoby pętlę
    /// uczenia samą na siebie i skasowało pracę użytkownika.
    /// </summary>
    [Fact]
    public async Task Przeliczenie_NIE_RUSZA_wierszy_z_decyzja_czlowieka()
    {
        var reczny = AddTransaction("lidl", TransactionStatus.ManuallyCategorized, _jedzenieId);
        var potwierdzony = AddTransaction("zabka", TransactionStatus.Confirmed, _jedzenieId);
        await _db.SaveChangesAsync();

        // Model twierdzi co innego niż człowiek — i ma zostać z tym dla siebie.
        _answers["lidl"] = new CategorySuggestion(_wynagrodzenieId, 0.99m);
        _answers["zabka"] = new CategorySuggestion(_wynagrodzenieId, 0.99m);

        var report = await RecategorizeHandler().HandleAsync(default);

        Assert.Equal(0, report.Examined);
        Assert.Equal(_jedzenieId, reczny.CategoryId);
        Assert.Equal(_jedzenieId, potwierdzony.CategoryId);
        Assert.Equal(TransactionStatus.ManuallyCategorized, reczny.Status);
        Assert.Equal(TransactionStatus.Confirmed, potwierdzony.Status);
    }

    [Fact]
    public async Task Wiersz_auto_dostaje_nowa_kategorie_gdy_model_zmienil_zdanie()
    {
        var transakcja = AddTransaction("zabka", TransactionStatus.AutoCategorized, _wynagrodzenieId, 0.8m);
        await _db.SaveChangesAsync();

        _answers["zabka"] = new CategorySuggestion(_jedzenieId, 0.95m);

        var report = await RecategorizeHandler().HandleAsync(default);

        Assert.Equal(1, report.Examined);
        Assert.Equal(1, report.Recategorized);
        Assert.Equal(0, report.MovedToReview);
        Assert.Equal(_jedzenieId, transakcja.CategoryId);
        Assert.Equal(0.95m, transakcja.Confidence);
        Assert.Equal(TransactionStatus.AutoCategorized, transakcja.Status);
    }

    /// <summary>
    /// Odwrotny kierunek: model, który dotąd zgadywał, przestaje. Wiersz MUSI stracić kategorię
    /// i wrócić do kolejki — inaczej zła kategoria zostałaby na zawsze, tylko już bez pewności,
    /// która by ją tłumaczyła.
    /// </summary>
    [Fact]
    public async Task Brak_odpowiedzi_modelu_odbiera_kategorie_i_wysyla_do_przegladu()
    {
        var transakcja = AddTransaction("dr max", TransactionStatus.AutoCategorized, _jedzenieId, 0.89m);
        await _db.SaveChangesAsync();

        // Opisu nie ma w mapie — atrapa odpowiada brakiem, tak jak model bez znanego słowa.

        var report = await RecategorizeHandler().HandleAsync(default);

        Assert.Equal(1, report.Recategorized);
        Assert.Equal(1, report.MovedToReview);
        Assert.Null(transakcja.CategoryId);
        Assert.Equal(TransactionStatus.PendingReview, transakcja.Status);
    }

    [Fact]
    public async Task Odpowiedz_ponizej_progu_wchodzi_jako_PODPOWIEDZ__nie_jako_pewnik()
    {
        // Ta sama zasada co w imporcie (`CommitImportCommandHandler.StatusFor`): próg rozstrzyga STATUS,
        // a nie to, czy podpowiedź w ogóle zostaje. Kategoria jest, ale wiersz dalej czeka
        // na człowieka — i to on, potwierdzając ją, dopiero nakarmi zbiór treningowy.
        var transakcja = AddTransaction("nieznane", TransactionStatus.PendingReview, null);
        await _db.SaveChangesAsync();

        _answers["nieznane"] = new CategorySuggestion(_jedzenieId, 0.5m);

        var report = await RecategorizeHandler().HandleAsync(default);

        Assert.Equal(_jedzenieId, transakcja.CategoryId);
        Assert.Equal(TransactionStatus.PendingReview, transakcja.Status);
        Assert.Equal(1, report.Recategorized);

        // Pewność zapisana — na liście widać wtedy, jak blisko progu był model, zamiast
        // pustego pola sugerującego, że w ogóle nie miał zdania.
        Assert.Equal(0.5m, transakcja.Confidence);
    }

    [Fact]
    public async Task Wiersz_bez_zmiany_nie_jest_liczony_jako_przeliczony()
    {
        AddTransaction("zabka", TransactionStatus.AutoCategorized, _jedzenieId, 0.9m);
        await _db.SaveChangesAsync();

        _answers["zabka"] = new CategorySuggestion(_jedzenieId, 0.9m);

        var report = await RecategorizeHandler().HandleAsync(default);

        Assert.Equal(1, report.Examined);
        Assert.Equal(0, report.Recategorized);
        Assert.Equal(1, report.Unchanged);
    }

    /// <summary>
    /// Przeliczanie trzyma tę samą blokadę co import — czyli PRZEZ CAŁY PRZEBIEG nie da się
    /// opublikować nowego modelu. Bez tego trening w trakcie przeliczania rozdzieliłby wynik
    /// na „przed" i „po" w obrębie jednej operacji.
    ///
    /// Blokada jest jednostronna (patrz <c>ModelStore.BeginImport</c>), więc sprawdzamy jej
    /// JEDYNY kierunek: pytamy o trening z wnętrza przebiegu, przez atrapę kategoryzatora.
    /// </summary>
    [Fact]
    public async Task W_trakcie_przeliczania_nie_da_sie_wystartowac_treningu()
    {
        AddTransaction("zabka", TransactionStatus.AutoCategorized, _jedzenieId, 0.9m);
        await _db.SaveChangesAsync();

        var asked = false;
        ModelWriteLease? granted = null;
        _categorizer.OnCall = () =>
        {
            asked = true;
            granted = _store.TryBeginTraining();
        };

        await RecategorizeHandler().HandleAsync(default);

        Assert.True(asked, "atrapa nie została wywołana — test nie sprawdził niczego");
        Assert.Null(granted);
    }
}
