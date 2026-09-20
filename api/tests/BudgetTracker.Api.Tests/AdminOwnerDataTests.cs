using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Admin.Commands;
using BudgetTracker.Api.Features.Admin.Contracts;
using BudgetTracker.Api.Features.Admin.Exceptions;
using BudgetTracker.Api.Features.Admin.Queries;
using BudgetTracker.Api.Features.Admin.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Administracyjne operacje na danych właściciela (konta): podsumowanie, dane bez właściciela, trwałe usunięcie
/// i przepisanie. Te reguły są wołane przez serwer tożsamości przy usuwaniu konta, więc błąd tutaj to albo dane
/// zostawione po skasowanym koncie, albo skasowane dane cudzego konta.
///
/// Wymaga `docker compose up -d db` oraz roli `budget_jobs` (patrz api/db/setup-rls-roles.sql).
/// </summary>
public sealed class AdminOwnerDataTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_admin_test;Username=budget;Password=budget_dev_only";

    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();

    private AppDbContext _db = null!;
    private OwnerDataService _service = null!;
    private int _categoryId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        var category = new Category("Testowa");
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        _categoryId = category.Id;

        _service = new OwnerDataService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await TestDatabase.DropAsync(TestConnection);
    }

    /// <summary>Jeden budżet z kompletem dzieci: 2 transakcje i po jednym wierszu w pozostałych tabelach.</summary>
    private async Task SeedOwnerAsync(Guid owner, DateTimeOffset createdAt)
    {
        var budget = new Budget("Budżet " + owner.ToString()[^4..], new DateOnly(2026, 9, 1), 0m, createdAt, "PLN", owner);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        var batch = new ImportBatch(budget.Id, "mbank", "wyciag.csv", 2, createdAt, owner);
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            _db.Transactions.Add(new Transaction(
                new DateOnly(2026, 9, 2 + i), -10m - i, "SKLEP " + i, createdAt, TransactionStatus.Confirmed,
                budgetBusinessId: budget.BusinessId, importBatchId: batch.Id, userId: owner));
        }

        _db.BudgetItems.Add(new BudgetItem(budget.BusinessId, _categoryId, 100m, new DateOnly(2026, 9, 1), 80, owner));
        _db.SavingsGoals.Add(new SavingsGoal(budget.BusinessId, 500m, new DateOnly(2026, 9, 1), createdAt, owner));
        _db.SavingsReservations.Add(new SavingsReservation(budget.BusinessId, "Wakacje", 300m, null, 1, createdAt, owner));
        _db.StandingOrders.Add(new StandingOrder(budget.BusinessId, "Czynsz", 1000m, StandingOrderRhythm.Monthly, null, createdAt, owner));
        _db.EpisodicOrders.Add(new EpisodicOrder(budget.BusinessId, "Remont", null, createdAt, owner));
        await _db.SaveChangesAsync();
    }

    private static readonly OwnerDataCountsResponseDto OneFullSet = new(
        Budgets: 1, BudgetItems: 1, Transactions: 2, ImportBatches: 1,
        SavingsGoals: 1, SavingsReservations: 1, StandingOrders: 1, EpisodicOrders: 1);

    private Task<IReadOnlyDictionary<Guid, OwnerDataCountsResponseDto>> CountsAsync() =>
        _service.AsSystemAsync(() => _service.CountByOwnerAsync(default), default);

    [Fact]
    public async Task Summary_counts_every_table_per_owner_and_gives_zeros_to_owners_without_data()
    {
        await SeedOwnerAsync(_alice, Now);
        await SeedOwnerAsync(_bob, Now);
        var stranger = Guid.CreateVersion7();

        var result = await new GetUserDataSummariesQueryHandler(_service)
            .HandleAsync(new UserDataSummaryRequestDto([_alice, stranger]), default);

        Assert.Equal(2, result.Users.Count);
        Assert.Equal(OneFullSet, result.Users.Single(u => u.UserId == _alice).Counts);
        Assert.Equal(OwnerDataCountsResponseDto.Empty, result.Users.Single(u => u.UserId == stranger).Counts);
        Assert.DoesNotContain(result.Users, u => u.UserId == _bob);
    }

    [Fact]
    public async Task Delete_removes_all_rows_of_that_owner_and_leaves_everyone_elses_untouched()
    {
        await SeedOwnerAsync(_alice, Now);
        await SeedOwnerAsync(_bob, Now);

        var deleted = await new DeleteOwnerDataCommandHandler(_service).HandleAsync(_alice, default);

        Assert.Equal(OneFullSet, deleted.Counts);
        var counts = await CountsAsync();
        Assert.False(counts.ContainsKey(_alice), "po usunięciu nic nie może zostać pod tym właścicielem");
        Assert.Equal(OneFullSet, counts[_bob]);
    }

    [Fact]
    public async Task Delete_takes_soft_deleted_rows_too()
    {
        await SeedOwnerAsync(_alice, Now);
        // Transakcja skasowana logicznie nadal jest daną konta — trwałe usunięcie konta ma ją zabrać.
        var tx = await _db.Transactions.FirstAsync(t => t.UserId == _alice);
        _db.Remove(tx);
        await _db.SaveChangesAsync();

        var deleted = await new DeleteOwnerDataCommandHandler(_service).HandleAsync(_alice, default);

        Assert.Equal(2, deleted.Counts.Transactions);
        Assert.Empty(await _db.Transactions.IgnoreQueryFilters().Where(t => t.UserId == _alice).ToListAsync());
    }

    [Fact]
    public async Task Delete_is_idempotent_so_a_failed_account_deletion_can_be_retried()
    {
        await SeedOwnerAsync(_alice, Now);
        var handler = new DeleteOwnerDataCommandHandler(_service);

        await handler.HandleAsync(_alice, default);
        var second = await handler.HandleAsync(_alice, default);

        Assert.Equal(OwnerDataCountsResponseDto.Empty, second.Counts);
    }

    [Fact]
    public async Task Reassign_moves_every_row_to_the_target_owner_and_deletes_nothing()
    {
        await SeedOwnerAsync(_alice, Now);
        await SeedOwnerAsync(_bob, Now);

        var moved = await new ReassignOwnerDataCommandHandler(_service)
            .HandleAsync(_alice, new ReassignOwnerRequestDto(_bob), default);

        Assert.Equal(OneFullSet, moved.Counts);
        var counts = await CountsAsync();
        Assert.False(counts.ContainsKey(_alice));
        Assert.Equal(2, counts[_bob].Budgets);
        Assert.Equal(4, counts[_bob].Transactions);
    }

    private async Task AddRuleAsync(string pattern, Guid owner)
    {
        _db.CategoryRules.Add(new CategoryRule(_categoryId, RuleDirection.Any, 10, owner, pattern: pattern));
        await _db.SaveChangesAsync();
    }

    private async Task<List<string?>> RulesOfAsync(Guid owner) =>
        await _db.CategoryRules.IgnoreQueryFilters().Where(r => r.UserId == owner).OrderBy(r => r.Pattern)
            .Select(r => r.Pattern).ToListAsync();

    [Fact]
    public async Task Delete_takes_the_owners_rules_too_but_never_the_shared_ones()
    {
        // Reguły należą do konta: po usunięciu konta nie mogą zostać jako niewidzialne wiersze bez właściciela.
        await AddRuleAsync("wspolna", CategoryRule.SharedUserId);
        await AddRuleAsync("alicja", _alice);
        await AddRuleAsync("bartek", _bob);

        await new DeleteOwnerDataCommandHandler(_service).HandleAsync(_alice, default);

        Assert.Empty(await RulesOfAsync(_alice));
        Assert.Equal(["bartek"], await RulesOfAsync(_bob));
        Assert.Equal(["wspolna"], await RulesOfAsync(CategoryRule.SharedUserId));
    }

    [Fact]
    public async Task Deleting_the_empty_owner_leaves_the_shared_rules_alone()
    {
        // ⚠️ „Dane bez właściciela" z pustym identyfikatorem da się skasować z ekranu administratora. Reguły wspólne mają
        // ten sam pusty UserId — ich skasowanie zabrałoby reguły bazowe WSZYSTKIM kontom.
        await AddRuleAsync("wspolna", CategoryRule.SharedUserId);

        await new DeleteOwnerDataCommandHandler(_service).HandleAsync(Guid.Empty, default);

        Assert.Equal(["wspolna"], await RulesOfAsync(CategoryRule.SharedUserId));
    }

    [Fact]
    public async Task Reassign_moves_the_owners_rules_and_leaves_the_shared_ones()
    {
        await AddRuleAsync("wspolna", CategoryRule.SharedUserId);
        await AddRuleAsync("alicja", _alice);

        await new ReassignOwnerDataCommandHandler(_service).HandleAsync(_alice, new ReassignOwnerRequestDto(_bob), default);

        Assert.Empty(await RulesOfAsync(_alice));
        Assert.Equal(["alicja"], await RulesOfAsync(_bob));
        Assert.Equal(["wspolna"], await RulesOfAsync(CategoryRule.SharedUserId));
    }

    [Fact]
    public async Task Shared_rules_do_not_make_the_empty_owner_show_up_as_an_orphan()
    {
        await AddRuleAsync("wspolna", CategoryRule.SharedUserId);

        Assert.DoesNotContain(Guid.Empty, (await CountsAsync()).Keys);
    }

    [Fact]
    public async Task Reassign_to_the_same_or_an_empty_owner_is_rejected()
    {
        await SeedOwnerAsync(_alice, Now);
        var handler = new ReassignOwnerDataCommandHandler(_service);

        await Assert.ThrowsAsync<OwnerReassignInvalidException>(
            () => handler.HandleAsync(_alice, new ReassignOwnerRequestDto(_alice), default));
        await Assert.ThrowsAsync<OwnerReassignInvalidException>(
            () => handler.HandleAsync(_alice, new ReassignOwnerRequestDto(Guid.Empty), default));

        Assert.Equal(OneFullSet, (await CountsAsync())[_alice]);
    }

    [Fact]
    public async Task Orphans_are_owners_missing_from_the_known_accounts_including_the_empty_id_from_before_login()
    {
        await SeedOwnerAsync(_alice, Now);
        await SeedOwnerAsync(Guid.Empty, Now.AddDays(-3));
        await SeedOwnerAsync(_bob, Now.AddDays(-1));

        var result = await new GetOrphanedDataQueryHandler(_service)
            .HandleAsync(new OrphanedDataRequestDto([_alice]), default);

        Assert.Equal(new[] { Guid.Empty, _bob }.Order(), result.Owners.Select(o => o.OwnerId).Order());
        Assert.DoesNotContain(result.Owners, o => o.OwnerId == _alice);
        Assert.All(result.Owners, o => Assert.Equal(OneFullSet, o.Counts));
    }

    [Fact]
    public async Task Orphans_report_the_latest_timestamp_of_the_owners_data()
    {
        await SeedOwnerAsync(_bob, Now.AddDays(-1));

        var result = await new GetOrphanedDataQueryHandler(_service)
            .HandleAsync(new OrphanedDataRequestDto([]), default);

        Assert.Equal(Now.AddDays(-1), Assert.Single(result.Owners).LastChangedAt);
    }

    [Fact]
    public async Task Without_any_data_there_are_no_orphans()
    {
        var result = await new GetOrphanedDataQueryHandler(_service)
            .HandleAsync(new OrphanedDataRequestDto([_alice]), default);

        Assert.Empty(result.Owners);
    }
}
