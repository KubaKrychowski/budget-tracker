using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categories.Commands;
using BudgetTracker.Api.Features.Categories.Contracts;
using BudgetTracker.Api.Features.Categories.Exceptions;
using BudgetTracker.Api.Features.Categories.Queries;
using BudgetTracker.Api.Features.Categories.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Własne kategorie kont: tworzenie, edycja, usuwanie i widoczność względem kategorii wspólnych oraz cudzych.
///
/// <para>
/// Handlery idą na kontekstach z podpiętym <c>ICurrentUserAccessor</c>, więc filtr Owner w EF działa tak jak w API.
/// Polityki RLS w Postgresie sprawdza osobno <c>CategoriesRlsTests</c>.
/// </para>
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class CategoriesTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_categories_test;Username=budget;Password=budget_dev_only";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();
    private AppDbContext _system = null!;

    public async Task InitializeAsync()
    {
        await TestDatabase.ResetAsync(TestConnection);
        _system = Context(null);
        await _system.Database.EnsureCreatedAsync();

        _system.Categories.AddRange(
            new Category("Jedzenie"),
            new Category("Wynagrodzenie", CategoryType.Income),
            new Category("Hobby Boba", CategoryType.Expense, _bob));
        await _system.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _system.DisposeAsync();
        await TestDatabase.DropAsync(TestConnection);
    }

    private AppDbContext Context(Guid? userId)
    {
        ICurrentUserAccessor? accessor = userId is { } id ? new FakeCurrentUserAccessor(id) : null;
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options, accessor);
    }

    private Task<CategoryResponseDto> CreateAsync(Guid user, string? name, CategoryType type = CategoryType.Expense)
    {
        var db = Context(user);
        return new CreateCategoryCommandHandler(db, new CategoryNames(db), new FakeCurrentUserAccessor(user))
            .HandleAsync(new CategoryRequestDto(name, type), default);
    }

    private Task<CategoryResponseDto> UpdateAsync(Guid user, Guid id, string? name, CategoryType type)
    {
        var db = Context(user);
        return new UpdateCategoryCommandHandler(db, new CategoryLookup(db), new CategoryNames(db), new CategoryUsage(db))
            .HandleAsync(id, new CategoryRequestDto(name, type), default);
    }

    private Task DeleteAsync(Guid user, Guid id)
    {
        var db = Context(user);
        return new DeleteCategoryCommandHandler(db, new CategoryLookup(db), new CategoryUsage(db)).HandleAsync(id, default);
    }

    private Task<IReadOnlyList<CategoryResponseDto>> ListAsync(Guid user)
    {
        var db = Context(user);
        return new GetCategoryListQueryHandler(db, new CategoryUsage(db)).HandleAsync(default);
    }

    private async Task<Guid> SharedIdAsync(string name) =>
        await _system.Categories.Where(c => c.Name == name).Select(c => c.BusinessId).SingleAsync();

    private async Task UseAsync(Guid user, Guid categoryId, decimal amount)
    {
        var key = await _system.Categories.Where(c => c.BusinessId == categoryId).Select(c => c.Id).SingleAsync();
        _system.Transactions.Add(new Transaction(
            new DateOnly(2026, 10, 1), amount, "TEST", default, TransactionStatus.Confirmed, categoryId: key, userId: user));
        await _system.SaveChangesAsync();
    }

    [Fact]
    public async Task A_created_category_belongs_to_its_creator_with_the_chosen_type()
    {
        var created = await CreateAsync(_alice, "  Korepetycje  ", CategoryType.Income);

        Assert.Equal("Korepetycje", created.Name);
        Assert.Equal(CategoryType.Income, created.Type);
        Assert.False(created.IsShared);
        Assert.Equal(_alice, await _system.Categories.Where(c => c.BusinessId == created.Id).Select(c => c.UserId).SingleAsync());
    }

    [Fact]
    public async Task A_user_sees_shared_and_own_categories_but_not_another_users()
    {
        await CreateAsync(_alice, "Zwierzęta");

        var alice = (await ListAsync(_alice)).Select(c => c.Name).ToList();
        var bob = (await ListAsync(_bob)).Select(c => c.Name).ToList();

        Assert.Equal(["Jedzenie", "Wynagrodzenie", "Zwierzęta"], alice);
        Assert.Equal(["Hobby Boba", "Jedzenie", "Wynagrodzenie"], bob);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task An_empty_name_is_rejected(string? name) =>
        await Assert.ThrowsAsync<CategoryNameRequiredException>(() => CreateAsync(_alice, name));

    [Fact]
    public async Task A_name_longer_than_the_column_is_rejected_instead_of_failing_in_the_database() =>
        await Assert.ThrowsAsync<CategoryNameTooLongException>(
            () => CreateAsync(_alice, new string('x', CategoryNames.MaxLength + 1)));

    [Theory]
    [InlineData("Jedzenie")]
    [InlineData("jedzenie")]
    public async Task A_name_taken_by_a_shared_category_is_rejected_ignoring_case(string name) =>
        // Indeks w bazie widzi tylko jednego właściciela, więc zderzenie ze wspólną nazwą łapie wyłącznie handler.
        await Assert.ThrowsAsync<CategoryNameTakenException>(() => CreateAsync(_alice, name));

    [Fact]
    public async Task A_name_taken_by_the_users_own_category_is_rejected()
    {
        await CreateAsync(_alice, "Zwierzęta");

        await Assert.ThrowsAsync<CategoryNameTakenException>(() => CreateAsync(_alice, "ZWIERZĘTA"));
    }

    [Fact]
    public async Task Two_accounts_may_each_have_a_category_with_the_same_name()
    {
        // Hobby Boba należy do Boba; Alicja widzi tylko wspólne, więc jej "Hobby Boba" nie koliduje.
        var created = await CreateAsync(_alice, "Hobby Boba");

        Assert.Equal("Hobby Boba", created.Name);
    }

    [Fact]
    public async Task A_shared_category_cannot_be_renamed_or_deleted()
    {
        var shared = await SharedIdAsync("Jedzenie");

        await Assert.ThrowsAsync<CategorySharedReadOnlyException>(
            () => UpdateAsync(_alice, shared, "Inne", CategoryType.Expense));
        await Assert.ThrowsAsync<CategorySharedReadOnlyException>(() => DeleteAsync(_alice, shared));
    }

    [Fact]
    public async Task Another_users_category_looks_like_it_does_not_exist()
    {
        var bobs = await SharedIdAsync("Hobby Boba");

        await Assert.ThrowsAsync<BudgetTracker.Api.Features.Categorization.Exceptions.CategoryNotFoundException>(
            () => DeleteAsync(_alice, bobs));
    }

    [Fact]
    public async Task An_unused_category_can_change_its_type_and_name()
    {
        var created = await CreateAsync(_alice, "Hobby", CategoryType.Expense);

        var updated = await UpdateAsync(_alice, created.Id, "Pasje", CategoryType.Income);

        Assert.Equal(("Pasje", CategoryType.Income), (updated.Name, updated.Type));
    }

    [Fact]
    public async Task A_used_category_can_be_renamed_but_not_retyped()
    {
        // Wpływ zmieniony na wydatek zostawiłby w kategorii transakcje o przeciwnym znaku.
        var created = await CreateAsync(_alice, "Korepetycje", CategoryType.Income);
        await UseAsync(_alice, created.Id, 120m);

        var renamed = await UpdateAsync(_alice, created.Id, "Lekcje", CategoryType.Income);
        await Assert.ThrowsAsync<CategoryTypeLockedException>(
            () => UpdateAsync(_alice, created.Id, "Lekcje", CategoryType.Expense));

        Assert.Equal("Lekcje", renamed.Name);
        Assert.Equal(1, renamed.Transactions);
        Assert.True(renamed.InUse);
    }

    [Fact]
    public async Task A_used_category_cannot_be_deleted()
    {
        var created = await CreateAsync(_alice, "Zwierzęta");
        await UseAsync(_alice, created.Id, -40m);

        await Assert.ThrowsAsync<CategoryInUseException>(() => DeleteAsync(_alice, created.Id));
    }

    [Fact]
    public async Task A_deleted_category_disappears_and_frees_its_name()
    {
        var created = await CreateAsync(_alice, "Zwierzęta");

        await DeleteAsync(_alice, created.Id);

        Assert.DoesNotContain("Zwierzęta", (await ListAsync(_alice)).Select(c => c.Name));
        Assert.Equal("Zwierzęta", (await CreateAsync(_alice, "Zwierzęta")).Name);
    }

    [Theory]
    [InlineData(CategoryType.Income, 100, true)]
    [InlineData(CategoryType.Income, -100, false)]
    [InlineData(CategoryType.Expense, -100, true)]
    [InlineData(CategoryType.Expense, 100, true)]
    public void An_income_category_takes_only_incoming_amounts_but_an_expense_one_also_takes_refunds(
        CategoryType type, int amount, bool accepted) =>
        Assert.Equal(accepted, CategoryTypeGuard.Accepts(type, amount));
}
