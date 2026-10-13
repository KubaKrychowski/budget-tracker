using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categories.Commands;
using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Features.Categories.Queries;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Transactions.Queries;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Ukrywanie kategorii wspólnych przez konto (zgłoszenie #30): komendy, listy wyboru i to, że kategoryzacja nie podpowiada
/// ukrytych. Dane są zmyślone. Wymaga `docker compose up -d db`. Izolację między kontami sprawdza <c>HiddenCategoriesRlsTests</c>.
/// </summary>
public sealed class HiddenCategoriesTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_hidden_test;Username=budget;Password=budget_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _user = Guid.CreateVersion7();

    private AppDbContext _db = null!;
    private Category _shared = null!;
    private Category _own = null!;

    public async Task InitializeAsync()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _shared = new Category("Paliwo");
        _own = new Category("Hobby", userId: _user);
        _db.Categories.AddRange(_shared, _own);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private HideCategoryCommandHandler Hide() => new(_db, new CategoryUsage(_db), new FakeCurrentUserAccessor(_user));

    private UnhideCategoryCommandHandler Unhide() => new(_db, new CategoryUsage(_db));

    private GetCategoriesQueryHandler Options() => new(_db, new HiddenCategories(_db));

    [Fact]
    public async Task Hiding_a_shared_category_marks_it_hidden_on_the_management_list()
    {
        await Hide().HandleAsync(_shared.BusinessId, default);

        var list = await new GetCategoryListQueryHandler(_db, new CategoryUsage(_db), new HiddenCategories(_db)).HandleAsync(default);

        Assert.True(list.Single(c => c.Id == _shared.BusinessId).Hidden);
        Assert.False(list.Single(c => c.Id == _own.BusinessId).Hidden);
    }

    [Fact]
    public async Task Hiding_an_own_category_is_rejected_because_it_can_be_renamed_or_deleted_instead()
    {
        await Assert.ThrowsAsync<CategoryHideNotSharedException>(() => Hide().HandleAsync(_own.BusinessId, default));

        Assert.Empty(await _db.HiddenCategories.ToListAsync());
    }

    [Fact]
    public async Task Hiding_an_unknown_category_is_not_found()
    {
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => Hide().HandleAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Hiding_twice_keeps_a_single_row_so_a_double_click_is_harmless()
    {
        await Hide().HandleAsync(_shared.BusinessId, default);
        await Hide().HandleAsync(_shared.BusinessId, default);

        // Łapie błąd unikalności: drugie ukrycie nie może wywalić się na indeksie ani zdublować wiersza.
        Assert.Single(await _db.HiddenCategories.ToListAsync());
    }

    [Fact]
    public async Task Unhiding_restores_the_category_and_it_can_be_hidden_again()
    {
        await Hide().HandleAsync(_shared.BusinessId, default);
        await Unhide().HandleAsync(_shared.BusinessId, default);

        Assert.Empty(await _db.HiddenCategories.ToListAsync());
        // Skasowane logicznie ukrycie nie może blokować ponownego ukrycia (indeks unikalny jest częściowy).
        await Hide().HandleAsync(_shared.BusinessId, default);
        Assert.Single(await _db.HiddenCategories.ToListAsync());
    }

    [Fact]
    public async Task Unhiding_a_category_that_was_never_hidden_is_a_no_op()
    {
        var result = await Unhide().HandleAsync(_shared.BusinessId, default);

        Assert.False(result.Hidden);
    }

    [Fact]
    public async Task The_selection_dictionary_skips_hidden_categories_unless_asked_for_them()
    {
        await Hide().HandleAsync(_shared.BusinessId, default);

        var visible = await Options().HandleAsync(includeHidden: false, default);
        var all = await Options().HandleAsync(includeHidden: true, default);

        Assert.Equal(["Hobby"], visible.Select(c => c.Name));
        // Z includeHidden ekran nadal potrafi nazwać nią istniejącą transakcję i wie, że to ukryta.
        Assert.True(all.Single(c => c.Name == "Paliwo").Hidden);
    }

    [Fact]
    public async Task Hiding_does_not_touch_existing_transactions_in_that_category()
    {
        _db.Transactions.Add(new Transaction(
            new DateOnly(2026, 10, 1), -100m, "STACJA", _clock.GetUtcNow(),
            BudgetTracker.Api.Domain.Consts.TransactionStatus.Confirmed, categoryId: _shared.Id));
        await _db.SaveChangesAsync();

        await Hide().HandleAsync(_shared.BusinessId, default);

        Assert.Equal(_shared.Id, (await _db.Transactions.SingleAsync()).CategoryId);
    }

    [Fact]
    public async Task Categorization_does_not_suggest_a_hidden_category()
    {
        // Baza bazowa ma reguły; „ZABKA” trafia do Jedzenia. ML nie jest potrzebny — reguła rozstrzyga pierwsza.
        // ⚠️ Seed pomija się, gdy w bazie są już jakiekolwiek kategorie — dlatego najpierw zdejmujemy te z przygotowania testu.
        _db.Categories.RemoveRange(_shared, _own);
        await _db.SaveChangesAsync();
        await BaselineSeed.SeedAsync(_db);
        var food = await _db.Categories.SingleAsync(c => c.Name == "Jedzenie");
        var hidden = new HiddenCategories(_db);
        var categorizer = new HybridCategorizer(new RuleCategorizer(_db), ml: null!, hidden);
        var normalized = DescriptionNormalizer.Normalize("ZABKA Z3489 K.2");

        var before = await categorizer.CategorizeAsync(normalized, "Obciążenie", -10m, default);
        await Hide().HandleAsync(food.BusinessId, default);
        var after = await new HybridCategorizer(new RuleCategorizer(_db), ml: null!, new HiddenCategories(_db))
            .CategorizeAsync(normalized, "Obciążenie", -10m, default);

        Assert.Equal(food.Id, before.CategoryId);
        // Ukryta kategoria z reguły daje „brak podpowiedzi” (wiersz idzie do przeglądu), a nie podmianę przez model.
        Assert.Null(after.CategoryId);
    }
}
