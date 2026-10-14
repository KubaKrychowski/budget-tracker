using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Dashboard.Consts;
using BudgetTracker.Api.Features.Dashboard.Queries;
using BudgetTracker.Api.Features.ProductCategories.Commands;
using BudgetTracker.Api.Features.ProductCategories.Contracts;
using BudgetTracker.Api.Features.ProductCategories.Exceptions;
using BudgetTracker.Api.Features.ProductCategories.Queries;
using BudgetTracker.Api.Features.ProductCategories.Services;
using BudgetTracker.Api.Features.Receipts.Commands;
using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Kategorie produktów z paragonów: podpowiedź regułami, uczenie z poprawek użytkownika, model ML.NET i rozbicie na wykresie wydatków.
/// Na prawdziwym Postgresie, jak reszta testów handlerów. Nazwy produktów i kwoty są zmyślone.
/// </summary>
public sealed class ProductCategoriesTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_productcat_test;Username=budget;Password=budget_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _userId = Guid.CreateVersion7();

    private AppDbContext _db = null!;
    private Guid _dairy;
    private Guid _bakery;

    public async Task InitializeAsync()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();
        await ProductBaselineSeed.SeedAsync(_db);

        _dairy = await IdOfAsync("Nabiał");
        _bakery = await IdOfAsync("Pieczywo");
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private Task<Guid> IdOfAsync(string name) =>
        _db.ProductCategories.Where(c => c.Name == name).Select(c => c.BusinessId).SingleAsync();

    private ProductCategorizer Categorizer(decimal threshold = 0.7m) => ProductTestKit.Categorizer(_db, _userId, threshold);

    private SetReceiptLineCategoryCommandHandler SetCategory() =>
        new(_db, new ProductRuleLearning(_db, new FakeCurrentUserAccessor(_userId)));

    private async Task<Receipt> AddReceiptAsync(params ReceiptLine[] items) => await AddReceiptAsync(null, items);

    private async Task<Receipt> AddReceiptAsync(Guid? transactionId, params ReceiptLine[] items)
    {
        var receipt = new Receipt(_userId, $"receipts/{Guid.NewGuid()}.jpg", "p.jpg", "image/jpeg", 1, _clock.GetUtcNow());
        receipt.SetItems(items);
        receipt.AttachTo(transactionId);
        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync();
        return receipt;
    }

    private static ReceiptLine Line(string name, decimal price) => new(name, 1m, price, price);

    [Fact]
    public async Task Every_seed_pattern_compiles_and_every_seed_category_has_a_rule()
    {
        // Łapie literówkę we wzorcu: zły wzorzec jest pomijany po cichu, więc słowo kluczowe by po prostu nie działało.
        var rules = await _db.ProductRules.ToListAsync();
        foreach (var rule in rules) Assert.NotNull(BudgetTracker.Api.Features.Categorization.Services.RuleMatcher.Compile(rule.Pattern));

        var withRules = rules.Select(r => r.ProductCategoryId).ToHashSet();
        Assert.All(await _db.ProductCategories.ToListAsync(), c => Assert.Contains(c.Id, withRules));
    }

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        var before = (await _db.ProductCategories.CountAsync(), await _db.ProductRules.CountAsync());

        await ProductBaselineSeed.SeedAsync(_db);

        Assert.Equal(before, (await _db.ProductCategories.CountAsync(), await _db.ProductRules.CountAsync()));
    }

    [Fact]
    public async Task A_seed_keyword_suggests_the_category_regardless_of_pack_size()
    {
        var suggestion = await Categorizer().CategorizeAsync("Mleko UHT 3,2% 1 l", CancellationToken.None);

        Assert.Equal(_dairy, suggestion.CategoryId);
        Assert.Equal(1m, suggestion.Confidence);
    }

    [Fact]
    public async Task A_product_nobody_knows_gets_no_category()
    {
        var suggestion = await Categorizer().CategorizeAsync("Zxqv wyrób nieznany", CancellationToken.None);

        // Łapie zgadywanie: reguły bez dopasowania i model bez danych nie mogą wymyślić kategorii.
        Assert.Null(suggestion.CategoryId);
    }

    [Fact]
    public async Task Choosing_a_category_confirms_the_line_and_teaches_the_same_name_to_the_next_receipt()
    {
        var first = await AddReceiptAsync(Line("Zxqv kremowy 500 g", 9m));

        var result = await SetCategory().HandleAsync(
            first.BusinessId, 0, new SetReceiptLineCategoryRequestDto(_bakery), CancellationToken.None);

        Assert.Equal((_bakery, true), (result.ProductCategoryId, result.CategoryConfirmed));
        // Inna gramatura tej samej nazwy: reguła nauczona nie może zależeć od opakowania.
        Assert.Equal(_bakery, (await Categorizer().CategorizeAsync("ZXQV kremowy 1 kg", CancellationToken.None)).CategoryId);
    }

    [Fact]
    public async Task A_learned_rule_beats_the_seed_keyword()
    {
        var receipt = await AddReceiptAsync(Line("Mleko migdałowe", 8m));

        await SetCategory().HandleAsync(receipt.BusinessId, 0, new SetReceiptLineCategoryRequestDto(_bakery), CancellationToken.None);

        // Słowo „mleko” jest w nabiale, ale użytkownik wie lepiej — inaczej poprawka nie miałaby jak zadziałać.
        Assert.Equal(_bakery, (await Categorizer().CategorizeAsync("Mleko migdałowe", CancellationToken.None)).CategoryId);
    }

    [Fact]
    public async Task Correcting_the_same_name_again_moves_the_one_rule_instead_of_adding_a_second()
    {
        var receipt = await AddReceiptAsync(Line("Zxqv kremowy", 9m));

        await SetCategory().HandleAsync(receipt.BusinessId, 0, new SetReceiptLineCategoryRequestDto(_bakery), CancellationToken.None);
        await SetCategory().HandleAsync(receipt.BusinessId, 0, new SetReceiptLineCategoryRequestDto(_dairy), CancellationToken.None);

        Assert.Equal(1, await _db.ProductRules.CountAsync(r => r.UserId == _userId));
        Assert.Equal(_dairy, (await Categorizer().CategorizeAsync("Zxqv kremowy", CancellationToken.None)).CategoryId);
    }

    [Fact]
    public async Task Clearing_the_category_forgets_the_learned_rule()
    {
        var receipt = await AddReceiptAsync(Line("Zxqv kremowy", 9m));
        await SetCategory().HandleAsync(receipt.BusinessId, 0, new SetReceiptLineCategoryRequestDto(_bakery), CancellationToken.None);

        var cleared = await SetCategory().HandleAsync(
            receipt.BusinessId, 0, new SetReceiptLineCategoryRequestDto(null), CancellationToken.None);

        Assert.Equal((null, false), (cleared.ProductCategoryId, cleared.CategoryConfirmed));
        Assert.Null((await Categorizer().CategorizeAsync("Zxqv kremowy", CancellationToken.None)).CategoryId);
    }

    [Fact]
    public async Task The_chosen_category_is_stored_with_the_receipt()
    {
        var receipt = await AddReceiptAsync(Line("Chleb", 4m), Line("Mleko", 3m));

        await SetCategory().HandleAsync(receipt.BusinessId, 1, new SetReceiptLineCategoryRequestDto(_dairy), CancellationToken.None);

        // Łapie zmianę, która nie przechodzi do kolumny jsonb: EF zauważa dopiero podmienioną listę.
        var stored = (await _db.Receipts.AsNoTracking().SingleAsync()).Items;
        Assert.Equal([null, _dairy], stored.Select(i => i.ProductCategoryId));
    }

    [Fact]
    public async Task An_unknown_category_is_a_bad_request_and_an_unknown_index_is_not_found()
    {
        var receipt = await AddReceiptAsync(Line("Chleb", 4m));

        await Assert.ThrowsAsync<ProductCategoryInvalidException>(() => SetCategory().HandleAsync(
            receipt.BusinessId, 0, new SetReceiptLineCategoryRequestDto(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<ReceiptItemNotFoundException>(() => SetCategory().HandleAsync(
            receipt.BusinessId, 5, new SetReceiptLineCategoryRequestDto(_dairy), CancellationToken.None));
    }

    [Fact]
    public async Task Lines_saved_before_categories_existed_still_load_as_uncategorized()
    {
        // Łapie wiersz jsonb sprzed zmiany: pozycje bez pól kategorii muszą się zmaterializować, nie wywrócić listy paragonów.
        var receipt = await AddReceiptAsync(Line("Chleb", 4m));
        var oldShape = "[{\"Name\":\"Chleb\",\"Quantity\":1,\"UnitPrice\":4,\"TotalPrice\":4}]";
        await _db.Database.ExecuteSqlAsync($"UPDATE \"Receipts\" SET \"Items\" = {oldShape}::jsonb");

        var loaded = (await _db.Receipts.AsNoTracking().SingleAsync(r => r.BusinessId == receipt.BusinessId)).Items.Single();

        Assert.Equal((null, false), (loaded.ProductCategoryId, loaded.CategoryConfirmed));
    }

    [Fact]
    public async Task Creating_a_category_rejects_an_empty_name_and_a_name_taken_by_a_shared_one()
    {
        var handler = new CreateProductCategoryCommandHandler(_db, new FakeCurrentUserAccessor(_userId));

        await Assert.ThrowsAsync<ProductCategoryNameRequiredException>(() =>
            handler.HandleAsync(new ProductCategoryRequestDto("  "), CancellationToken.None));
        await Assert.ThrowsAsync<ProductCategoryNameTakenException>(() =>
            handler.HandleAsync(new ProductCategoryRequestDto("nabiał"), CancellationToken.None));

        var created = await handler.HandleAsync(new ProductCategoryRequestDto(" Przyprawy "), CancellationToken.None);
        Assert.Equal(("Przyprawy", false), (created.Name, created.IsShared));
        Assert.Contains(created.Id, (await new GetProductCategoryListQueryHandler(_db).HandleAsync(CancellationToken.None)).Select(c => c.Id));
    }

    [Fact]
    public async Task The_model_stays_silent_until_the_user_has_confirmed_enough_examples()
    {
        // Łapie model uczony na kilku przykładach, który podpowiada z całkowitą pewnością.
        await ConfirmAsync(_dairy, ["Zxqv jeden", "Zxqv dwa"]);

        Assert.Null((await Categorizer(threshold: 0.1m).CategorizeAsync("Zxqv trzy", CancellationToken.None)).CategoryId);
    }

    [Fact]
    public async Task Once_trained_the_model_suggests_a_category_for_a_similar_unseen_name()
    {
        await ConfirmAsync(_dairy, Enumerable.Range(0, 12).Select(i => $"Zxqv mlecz{(char)('a' + i)}"));
        await ConfirmAsync(_bakery, Enumerable.Range(0, 12).Select(i => $"Qwvx pieczyw{(char)('a' + i)}"));

        var dairy = await Categorizer(threshold: 0.5m).CategorizeAsync("Zxqv mleczz", CancellationToken.None);
        var bakery = await Categorizer(threshold: 0.5m).CategorizeAsync("Qwvx pieczywz", CancellationToken.None);

        Assert.Equal((_dairy, _bakery), (dairy.CategoryId, bakery.CategoryId));
    }

    [Fact]
    public async Task A_suggestion_the_user_never_confirmed_does_not_train_the_model()
    {
        // Łapie sprzężenie zwrotne: model uczący się z własnych podpowiedzi powtarzałby swoje pomyłki z rosnącą pewnością.
        var lines = Enumerable.Range(0, 30)
            .Select(i => new ReceiptLine($"Zxqv nazwa{(char)('a' + i % 20)}", 1m, 1m, 1m, i % 2 == 0 ? _dairy : _bakery))
            .ToArray();
        await AddReceiptAsync(lines);

        Assert.Null((await Categorizer(threshold: 0.1m).CategorizeAsync("Zxqv nazwaa", CancellationToken.None)).CategoryId);
    }

    private async Task ConfirmAsync(Guid category, IEnumerable<string> names)
    {
        var receipt = await AddReceiptAsync([.. names.Select(n => Line(n, 1m))]);
        for (var i = 0; i < receipt.Items.Count; i++)
        {
            await SetCategory().HandleAsync(receipt.BusinessId, i, new SetReceiptLineCategoryRequestDto(category), CancellationToken.None);
        }
    }

    [Fact]
    public async Task The_dashboard_splits_a_category_into_products_and_still_adds_up_to_the_bar()
    {
        var food = new Category("Jedzenie");
        _db.Categories.Add(food);
        var budget = new Budget("Testowy", new DateOnly(2026, 10, 1), 0m, _clock.GetUtcNow());
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        var withReceipt = Expense(budget, food, -30m, new DateOnly(2026, 10, 5));
        var withoutReceipt = Expense(budget, food, -20m, new DateOnly(2026, 10, 6));
        _db.Transactions.AddRange(withReceipt, withoutReceipt);
        await _db.SaveChangesAsync();
        await AddReceiptAsync(
            withReceipt.BusinessId,
            new ReceiptLine("Mleko", 1m, 10m, 10m, _dairy), new ReceiptLine("Chleb", 1m, 15m, 15m, _bakery),
            new ReceiptLine("Zxqv", 1m, 5m, 5m));

        var response = await new GetDashboardQueryHandler(_db, Localizer())
            .HandleAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), budget.BusinessId, CancellationToken.None);

        var spend = Assert.Single(response.ByCategory, c => c.CategoryName == "Jedzenie");
        Assert.Equal(50m, spend.Amount);
        Assert.Equal(
            [("Pieczywo", 15m), ("Nabiał", 10m), (null, 5m), (null, 20m)],
            spend.Products.Select(p => (p.Name, p.Amount)));
        Assert.Equal(
            [ProductSpendKind.Category, ProductSpendKind.Category, ProductSpendKind.Uncategorized, ProductSpendKind.NoReceipt],
            spend.Products.Select(p => p.Kind));
        Assert.Equal(spend.Amount, spend.Products.Sum(p => p.Amount));
    }

    [Fact]
    public async Task A_category_with_no_receipts_has_no_breakdown()
    {
        var food = new Category("Jedzenie");
        _db.Categories.Add(food);
        var budget = new Budget("Testowy", new DateOnly(2026, 10, 1), 0m, _clock.GetUtcNow());
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
        _db.Transactions.Add(Expense(budget, food, -20m, new DateOnly(2026, 10, 6)));
        await _db.SaveChangesAsync();

        var response = await new GetDashboardQueryHandler(_db, Localizer())
            .HandleAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), budget.BusinessId, CancellationToken.None);

        Assert.Empty(Assert.Single(response.ByCategory).Products);
    }

    private Transaction Expense(Budget budget, Category category, decimal amount, DateOnly date) =>
        new(date, amount, "ZAKUP", _clock.GetUtcNow(), TransactionStatus.Confirmed, categoryId: category.Id, budgetBusinessId: budget.BusinessId);

    private static IStringLocalizer<SharedResource> Localizer() => new StringLocalizer<SharedResource>(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));
}
