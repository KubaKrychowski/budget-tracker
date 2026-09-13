using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Testy bramki „model nie zna ANI JEDNEGO słowa z tego opisu".
///
/// Zbiór treningowy jest tu syntetyczny, ale odtwarza dokładnie ten skos, który wywołał
/// błąd na realnych danych: jeden typ operacji („karta") jest zdominowany przez jedną
/// kategorię. Dzięki temu model dostaje silny sygnał z typu i przy nieznanym opisie odpowiada
/// tą kategorią z wysoką pewnością — a więc powyżej progu 0,7, czyli bez przeglądu.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class MlCategorizerTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_ml_test;Username=budget;Password=budget_dev_only";

    private const string SkewedType = "karta";
    private const string NeutralType = "sklep";

    private AppDbContext _db = null!;
    private string _dir = null!;
    private string _modelPath = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        // Nazwy kategorii MUSZĄ zgadzać się z etykietami zbioru — model mówi nazwami,
        // baza identyfikatorami, a mapowanie powstaje z bazy.
        _db.Categories.AddRange(
            new Category("Subskrypcje"),
            new Category("Jedzenie"));
        await _db.SaveChangesAsync();

        _dir = Directory.CreateTempSubdirectory("ml-categorizer-test").FullName;
        _modelPath = Path.Combine(_dir, "model.zip");
        CategoryModelTrainer.Train(TrainingRows(), _modelPath);
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    /// <summary>
    /// Zbiór ze skosem: „karta" to niemal same Subskrypcje, „sklep" — samo Jedzenie.
    /// Opisy w obu grupach są rozłączne słownikowo.
    /// </summary>
    private static List<TransactionFeatures> TrainingRows()
    {
        var rows = new List<TransactionFeatures>();

        for (var i = 0; i < 40; i++)
            rows.Add(new TransactionFeatures
            {
                Description = $"spotify subskrypcja {i}",
                TransactionType = SkewedType,
                Amount = -25f,
                Category = "Subskrypcje",
            });

        for (var i = 0; i < 40; i++)
            rows.Add(new TransactionFeatures
            {
                Description = $"biedronka sklep {i}",
                TransactionType = NeutralType,
                Amount = -25f,
                Category = "Jedzenie",
            });

        return rows;
    }

    private MlCategorizer Categorizer() => new(_db, Options.Create(new CategorizationOptions
    {
        ModelPath = _modelPath,
        TrainingDataPath = Path.Combine(_dir, "nieistotne.csv"),
    }));

    /// <summary>
    /// MUTACJA W FORMIE TESTU: dowodzi, że bez bramki ten sam wiersz wchodziłby automatycznie.
    ///
    /// Gdyby ten test przestał przechodzić, znaczyłoby to, że sam model przestał być pewny
    /// nieznanych opisów — a wtedy testy niżej przechodziłyby z niewłaściwego powodu
    /// i nie pilnowałyby już niczego.
    /// </summary>
    [Fact]
    public void Sam_model_jest_PEWNY_nieznanego_opisu__i_to_jest_ten_blad()
    {
        var ml = new MLContext();
        using var stream = File.OpenRead(_modelPath);
        using var engine = ml.Model.CreatePredictionEngine<TransactionFeatures, CategoryPrediction>(
            ml.Model.Load(stream, out _));

        var prediction = engine.Predict(new TransactionFeatures
        {
            Description = "zzzz wwww",
            TransactionType = SkewedType,
            Amount = -25f,
        });

        Assert.Equal("Subskrypcje", prediction.Category);
        Assert.True(prediction.Score.Max() > 0.7f,
            $"model miał odpowiedzieć pewnie, a dał {prediction.Score.Max():F3} — test niżej nic już nie dowodzi");
    }

    [Fact]
    public async Task Opis_bez_ANI_JEDNEGO_znanego_slowa_nie_dostaje_kategorii()
    {
        using var categorizer = Categorizer();

        var suggestion = await categorizer.CategorizeAsync("zzzz wwww", SkewedType, -25m, default);

        Assert.Null(suggestion.CategoryId);
    }

    /// <summary>Pusty opis to skrajny przypadek tego samego — decydowałby sam typ operacji.</summary>
    [Fact]
    public async Task Pusty_opis_nie_dostaje_kategorii()
    {
        using var categorizer = Categorizer();

        var suggestion = await categorizer.CategorizeAsync("", SkewedType, -25m, default);

        Assert.Null(suggestion.CategoryId);
    }

    [Fact]
    public async Task Znany_sprzedawca_dalej_wchodzi_automatycznie()
    {
        using var categorizer = Categorizer();

        var suggestion = await categorizer.CategorizeAsync("spotify subskrypcja 7", SkewedType, -25m, default);

        Assert.NotNull(suggestion.CategoryId);
        Assert.True(suggestion.Confidence >= 0.7m, $"pewność spadła do {suggestion.Confidence}");
    }

    /// <summary>
    /// Bramka pyta o SŁOWA, nie o całe opisy. Wiersz z jednym znanym słowem i resztą nieznaną
    /// ma przejść dalej — o jego losie ma decydować próg pewności, tak jak dotąd.
    /// </summary>
    [Fact]
    public async Task Jedno_znane_slowo_wystarczy__o_reszcie_decyduje_prog()
    {
        using var categorizer = Categorizer();

        var suggestion = await categorizer.CategorizeAsync("spotify qqqqq wwwww", SkewedType, -25m, default);

        Assert.NotNull(suggestion.CategoryId);
    }

    /// <summary>
    /// Próg pewności PRZEPROWADZIŁ SIĘ z sekcji <c>Import</c> do <c>Categorization</c>
    /// (zapytać model umie już nie tylko import). Przeprowadzka nazwy klucza w konfiguracji
    /// jest cicha: zostawiona sekcja <c>Import</c> nie jest błędem, tylko martwym wpisem,
    /// a próg po cichu wraca do wartości domyślnej — akurat też 0,7, więc nie widać niczego.
    ///
    /// Dlatego test czyta PRAWDZIWY plik konfiguracyjny, a nie atrapę.
    /// </summary>
    [Fact]
    public void Prog_pewnosci_wiaze_sie_z_pliku_konfiguracyjnego()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var section = config.GetSection(CategorizationOptions.SectionName);
        Assert.True(section.GetSection(nameof(CategorizationOptions.ConfidenceThreshold)).Exists(),
            "próg pewności zniknął z sekcji Categorization — wróciłby po cichu do wartości domyślnej");

        var options = new CategorizationOptions();
        section.Bind(options);

        Assert.Equal(0.7m, options.ConfidenceThreshold);
    }

    /// <summary>
    /// TRIPWIRE na nazewnictwo z wnętrza ML.NET.
    ///
    /// Bramka rozpoznaje blok słowny po prefiksie slotu „Word.". Gdyby aktualizacja biblioteki
    /// zmieniła to nazewnictwo, bramka nie znalazłaby granicy i — celowo — przestałaby blokować
    /// cokolwiek. Testy wyżej złapałyby to zachowaniem; ten łapie PRZYCZYNĘ, więc mówi wprost,
    /// czego szukać.
    ///
    /// Fakt jest tu sprawdzany niezależnie od kodu produkcyjnego (własne przejście po slotach),
    /// bo tripwire powtarzający implementację nie pilnowałby niczego.
    /// </summary>
    [Fact]
    public void Model_wystawia_blok_cech_slownych_pod_znana_nazwa()
    {
        var ml = new MLContext();
        using var stream = File.OpenRead(_modelPath);
        using var engine = ml.Model.CreatePredictionEngine<TransactionFeatures, CategoryPrediction>(
            ml.Model.Load(stream, out _));

        var column = engine.OutputSchema.GetColumnOrNull("DescriptionFeatures");
        Assert.NotNull(column);
        Assert.True(column!.Value.HasSlotNames(), "kolumna cech opisu nie niesie nazw slotów");

        VBuffer<ReadOnlyMemory<char>> slots = default;
        column.Value.GetSlotNames(ref slots);
        var names = slots.DenseValues().Select(n => n.ToString()).ToList();

        Assert.Contains(names, n => n.StartsWith("Word.", StringComparison.Ordinal));
        Assert.Contains(names, n => n.StartsWith("Char.", StringComparison.Ordinal));
    }
}
