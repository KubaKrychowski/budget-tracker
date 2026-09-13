using System.Globalization;
using System.Text;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Bramka słownika a słowa, które są w KAŻDYM opisie (zgłoszenie #9, druga odsłona).
///
/// <para>
/// Parser PKO dokleja do każdej płatności kartą „Miasto: …". Pierwsza wersja bramki pytała, czy model zna
/// choć jedno słowo opisu — a „miasto" znał zawsze, więc przepuszczała każdą płatność kartą, także losowy
/// ciąg znaków. Na realnym modelu dawało to „Subskrypcje" z pewnością 0,72–0,81, bez przeglądu.
/// </para>
///
/// <para>
/// Fikstura odtwarza oba składniki: słowo „miasto" w każdej z pięciu kategorii i typ operacji „karta",
/// który w zbiorze jest prawie wyłącznie Subskrypcjami. Nazwy sprzedawców są zmyślone.
/// </para>
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class MlCategorizerBoilerplateTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_mlboilerplate_test;Username=budget;Password=budget_dev_only";

    private const string SkewedType = "karta";
    private const string NeutralType = "sklep";

    /// <summary>Kategoria → zmyślony sprzedawca. „miasto" dochodzi do każdego opisu.</summary>
    private static readonly (string Category, string Merchant, string Type)[] Groups =
    [
        ("Subskrypcje", "strumyk", SkewedType),
        ("Jedzenie", "piekarnia", NeutralType),
        ("Paliwo", "tankownia", NeutralType),
        ("Odzież", "szwalnia", NeutralType),
        ("Hobby", "modelarnia", NeutralType),
    ];

    private AppDbContext _db = null!;
    private string _dir = null!;
    private string _modelPath = null!;
    private string _trainingPath = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();

        _db.Categories.AddRange(Groups.Select(g => new Category(g.Category)));
        await _db.SaveChangesAsync();

        _dir = Directory.CreateTempSubdirectory("ml-boilerplate-test").FullName;
        _modelPath = Path.Combine(_dir, "model.zip");
        _trainingPath = Path.Combine(_dir, "training-set.csv");

        var rows = TrainingRows();
        CategoryModelTrainer.Train(rows, _modelPath);
        WriteTrainingFile(rows, _trainingPath);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static List<TransactionFeatures> TrainingRows()
    {
        var rows = new List<TransactionFeatures>();
        foreach (var (category, merchant, type) in Groups)
        {
            var count = type == SkewedType ? 60 : 20;
            for (var i = 0; i < count; i++)
            {
                rows.Add(new TransactionFeatures
                {
                    Description = $"{merchant} miasto",
                    TransactionType = type,
                    Amount = -25f,
                    Category = category,
                });
            }
        }

        return rows;
    }

    /// <summary>Ten sam format, który czyta <see cref="TrainingSetBuilder"/> z pliku bazowego.</summary>
    private static void WriteTrainingFile(IEnumerable<TransactionFeatures> rows, string path)
    {
        var text = new StringBuilder("\"opis\",\"typ_transakcji\",\"kwota\",\"kategoria\",\"duzy_wydatek\"\n");
        foreach (var r in rows)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"\"{r.Description}\",\"{r.TransactionType}\",\"{r.Amount.ToString(CultureInfo.InvariantCulture)}\",\"{r.Category}\",\"0\"\n");
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private MlCategorizer Categorizer(string trainingPath)
    {
        var options = Options.Create(new CategorizationOptions
        {
            ModelPath = _modelPath,
            TrainingDataPath = trainingPath,
        });
        return new MlCategorizer(_db, options, new TrainingSetBuilder(_db, options));
    }

    /// <summary>
    /// MUTACJA W FORMIE TESTU: sam model jest pewny losowego opisu z „miasto" — i „miasto" jest w słowniku.
    /// Bez tego testy niżej przechodziłyby z niewłaściwego powodu (np. bo model przestał być pewny).
    /// </summary>
    [Fact]
    public void Sam_model_jest_PEWNY_losowego_opisu_z_rozlanym_slowem()
    {
        var ml = new MLContext();
        using var stream = File.OpenRead(_modelPath);
        using var engine = ml.Model.CreatePredictionEngine<TransactionFeatures, CategoryPrediction>(
            ml.Model.Load(stream, out _));

        var prediction = engine.Predict(new TransactionFeatures
        {
            Description = "zzzz wwww miasto",
            TransactionType = SkewedType,
            Amount = -25f,
        });

        Assert.Equal("Subskrypcje", prediction.Category);
        Assert.True(prediction.Score.Max() > 0.7f,
            $"model miał być pewny, a dał {prediction.Score.Max():F3} — testy niżej nic już nie dowodzą");
    }

    [Fact]
    public async Task Losowy_opis_ze_slowem_z_kazdej_kategorii_nie_dostaje_kategorii()
    {
        // Sedno #9: jedynym znanym słowem jest „miasto", obecne w 5 z 5 kategorii — nic nie mówi o sprzedawcy.
        using var categorizer = Categorizer(_trainingPath);

        var suggestion = await categorizer.CategorizeAsync("zzzz wwww miasto", SkewedType, -25m, default);

        Assert.Null(suggestion.CategoryId);
    }

    [Fact]
    public async Task Znany_sprzedawca_z_tym_samym_slowem_dalej_wchodzi_automatycznie()
    {
        // Bramka nie może zabrać automatu temu, co model naprawdę zna: „strumyk" jest w jednej kategorii.
        using var categorizer = Categorizer(_trainingPath);

        var suggestion = await categorizer.CategorizeAsync("strumyk miasto", SkewedType, -25m, default);

        Assert.NotNull(suggestion.CategoryId);
        Assert.True(suggestion.Confidence >= 0.7m, $"pewność spadła do {suggestion.Confidence}");
    }

    [Fact]
    public async Task Bez_danych_treningowych_bramka_liczy_kazde_znane_slowo_jak_dotad()
    {
        // ⚠️ Świadoma degradacja, nie przypadek: bez zbioru nie da się policzyć rozlania słów, więc bramka
        // wraca do „dowolne znane słowo" zamiast wysyłać wszystko do przeglądu. Ten test ją DOKUMENTUJE.
        using var categorizer = Categorizer(Path.Combine(_dir, "nie-ma-takiego-pliku.csv"));

        var suggestion = await categorizer.CategorizeAsync("zzzz wwww miasto", SkewedType, -25m, default);

        Assert.NotNull(suggestion.CategoryId);
    }
}
