using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets;
using BudgetTracker.Api.Features.Budgets.Commands;
using BudgetTracker.Api.Features.Budgets.Consts;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Budgets.Queries;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Ekran „Ustawienia → Budżety": lista, edycja, wyłączenie, reset, usunięcie, przywrócenie
/// i sprzątanie po oknie retencji. Każdy test odpowiada kryterium akceptacji z zadania.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class BudgetSettingsTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_budgetadmin_test;Username=budget;Password=budget_dev_only;Pooling=false";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly BudgetOptions _options = new() { RetentionDays = 30 };
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = NewContext();
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;

        return new AppDbContext(options);
    }

    private BudgetLookup Lookup() => new(_db);
    private BudgetChildren Children() => new(_db);
    private BudgetListItemReader Reader() => new(_db);

    private GetBudgetsListQueryHandler List() => new(Reader(), Options.Create(_options));
    private UpdateBudgetCommandHandler Update() => new(_db, Lookup(), Reader());
    private SetBudgetEnabledCommandHandler SetEnabled() => new(_db, Lookup(), Reader(), _clock);
    private ResetBudgetCommandHandler Reset() => new(_db, Lookup(), Children(), Reader(), _clock);
    private DeleteBudgetCommandHandler Delete() => new(_db, Lookup(), Children(), _clock);
    private RestoreBudgetCommandHandler Restore() => new(_db, Lookup(), Children(), Reader());
    private PurgeDeletedBudgetsCommandHandler Purge() => new(
        _db, Options.Create(_options), _clock,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PurgeDeletedBudgetsCommandHandler>.Instance);

    /// <summary>
    /// Budżet z pełnym kompletem dzieci — bez nich reset i usunięcie nie miałyby czego ruszyć,
    /// a testy przechodziłyby na pustym zbiorze.
    /// </summary>
    private async Task<Budget> SeedBudgetAsync(string name = "Testowy", decimal initialBalance = 1000m)
    {
        var category = new Category($"Kategoria {name}");
        var account = new Account($"Konto {name}", AccountType.Bank);
        var budget = new Budget(name, new DateOnly(2026, 9, 1), initialBalance, _clock.GetUtcNow());
        _db.AddRange(category, account, budget);
        await _db.SaveChangesAsync();

        var batch = new ImportBatch(budget.Id, "pko", "wyciag.csv", 0, _clock.GetUtcNow());
        _db.Add(batch);
        _db.Add(new BudgetItem(budget.BusinessId, category.Id, 500m));
        await _db.SaveChangesAsync();

        _db.AddRange(
            NewTransaction(budget, account, category, batch, -120.50m, 10),
            NewTransaction(budget, account, category, batch, -79.50m, 11));
        await _db.SaveChangesAsync();

        return budget;
    }

    private Transaction NewTransaction(
        Budget budget, Account account, Category category, ImportBatch batch, decimal amount, int day) =>
        new(
            new DateOnly(2026, 9, day),
            amount,
            $"Wydatek {day}",
            _clock.GetUtcNow(),
            TransactionStatus.AutoCategorized,
            categoryId: category.Id,
            budgetBusinessId: budget.BusinessId,
            accountId: account.Id,
            importBatchId: batch.Id);

    // ── Lista ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_returns_balance_as_initial_plus_transactions()
    {
        await SeedBudgetAsync(initialBalance: 1000m);

        var row = Assert.Single(await List().HandleAsync(default));

        Assert.Equal(1000m, row.InitialBalance);
        Assert.Equal(800m, row.Balance);          // 1000 - 120.50 - 79.50
        Assert.Equal(2, row.TransactionCount);
        Assert.Equal(500m, row.MonthlyLimit);
        Assert.Equal(BudgetStatus.Active, row.Status);
    }

    [Fact]
    public async Task List_shows_deleted_budgets_although_the_rest_of_the_app_does_not()
    {
        // To jedyne miejsce z IgnoreQueryFilters. Bez tego 30-dniowa odwracalność nie miałaby
        // gdzie zaistnieć — usunięty budżet zniknąłby także z ekranu, na którym się go przywraca.
        var budget = await SeedBudgetAsync();
        await Delete().HandleAsync(budget.BusinessId, default);

        var row = Assert.Single(await List().HandleAsync(default));
        Assert.Equal(BudgetStatus.Deleted, row.Status);

        Assert.Empty(await _db.Budgets.ToListAsync());
    }

    [Fact]
    public async Task Unknown_business_id_is_not_found_never_a_silent_fallback()
    {
        await SeedBudgetAsync();

        await Assert.ThrowsAsync<BudgetNotFoundException>(() =>
            SetEnabled().HandleAsync(Guid.NewGuid(), enabled: false, default));
    }

    // ── Edycja ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Editing_initial_balance_moves_the_whole_balance()
    {
        // Powód, dla którego UI ostrzega przed zapisem: to nie jest zmiana jednej liczby,
        // tylko przesunięcie punktu, od którego liczy się cały budżet.
        var budget = await SeedBudgetAsync(initialBalance: 1000m);

        var row = await Update().HandleAsync(
            budget.BusinessId, new UpdateBudgetRequestDto("Po zmianie", 2000m), default);

        Assert.Equal("Po zmianie", row.Name);
        Assert.Equal(2000m, row.InitialBalance);
        Assert.Equal(1800m, row.Balance);
    }

    [Fact]
    public async Task Empty_name_is_rejected()
    {
        var budget = await SeedBudgetAsync();

        await Assert.ThrowsAsync<BudgetNameRequiredException>(() =>
            Update().HandleAsync(budget.BusinessId, new UpdateBudgetRequestDto("   ", 0m), default));
    }

    // ── Wyłączenie ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Disabled_budget_stays_visible_and_manageable()
    {
        // Granica, którą najłatwiej przesunąć przez pomyłkę: wyłączenie zamyka budżet
        // na nowe dane, a NIE na zarządzanie nim.
        var budget = await SeedBudgetAsync();

        var disabled = await SetEnabled().HandleAsync(budget.BusinessId, enabled: false, default);
        Assert.Equal(BudgetStatus.Disabled, disabled.Status);
        Assert.NotNull(disabled.DisabledAt);

        // Filtr globalny go nie dotyczy — to nie jest DeletedAt.
        Assert.NotNull(await _db.Budgets.FirstOrDefaultAsync(b => b.BusinessId == budget.BusinessId));

        // …i dalej daje się nim zarządzać.
        var renamed = await Update().HandleAsync(
            budget.BusinessId, new UpdateBudgetRequestDto("Nadal edytowalny", 1000m), default);
        Assert.Equal("Nadal edytowalny", renamed.Name);
        Assert.Equal(BudgetStatus.Disabled, renamed.Status);

        var enabled = await SetEnabled().HandleAsync(budget.BusinessId, enabled: true, default);
        Assert.Equal(BudgetStatus.Active, enabled.Status);
        Assert.Null(enabled.DisabledAt);
    }

    // ── Reset ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reset_clears_children_and_keeps_the_budget_with_its_initial_balance()
    {
        var budget = await SeedBudgetAsync(initialBalance: 1000m);

        var row = await Reset().HandleAsync(budget.BusinessId, default);

        Assert.Equal(BudgetStatus.Active, row.Status);
        Assert.Equal(0, row.TransactionCount);
        Assert.Equal(1000m, row.InitialBalance);
        Assert.Equal(1000m, row.Balance);          // bilans wraca do punktu startu
        Assert.Equal(0m, row.MonthlyLimit);

        Assert.Empty(await _db.Transactions.ToListAsync());
        Assert.Empty(await _db.ImportBatches.ToListAsync());
        Assert.Empty(await _db.BudgetItems.ToListAsync());
    }

    [Fact]
    public async Task Reset_only_touches_its_own_budget()
    {
        var mine = await SeedBudgetAsync("Mój");
        var other = await SeedBudgetAsync("Cudzy");

        await Reset().HandleAsync(mine.BusinessId, default);

        var rows = await List().HandleAsync(default);
        Assert.Equal(0, rows.Single(b => b.Id == mine.BusinessId).TransactionCount);
        Assert.Equal(2, rows.Single(b => b.Id == other.BusinessId).TransactionCount);
    }

    // ── Usunięcie i przywrócenie ───────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_stamps_the_budget_and_its_children_without_removing_rows()
    {
        var budget = await SeedBudgetAsync();

        await Delete().HandleAsync(budget.BusinessId, default);

        // Nic nie zniknęło fizycznie — dlatego da się to cofnąć.
        Assert.Equal(2, await _db.Transactions.IgnoreQueryFilters().CountAsync());
        Assert.Empty(await _db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Restore_brings_back_the_budget_with_its_children()
    {
        var budget = await SeedBudgetAsync(initialBalance: 1000m);

        await Delete().HandleAsync(budget.BusinessId, default);
        var row = await Restore().HandleAsync(budget.BusinessId, default);

        Assert.Equal(BudgetStatus.Active, row.Status);
        Assert.Equal(2, row.TransactionCount);
        Assert.Equal(800m, row.Balance);
        Assert.Equal(500m, row.MonthlyLimit);
    }

    [Fact]
    public async Task Restore_brings_back_children_even_when_the_clock_moves_between_reads()
    {
        // ⚠️ Regresja znaleziona przy refactorze: usunięcie czytało zegar DWA RAZY — raz dla
        // dzieci, raz dla budżetu. Przywracanie rozpoznaje dzieci po RÓWNOŚCI znaczników, więc
        // na prawdziwym zegarze budżet wracał bez transakcji. Zatrzymany FakeTimeProvider tego
        // nie widział — ten test przesuwa zegar przy każdym odczycie.
        var budget = await SeedBudgetAsync(initialBalance: 1000m);
        _clock.AutoAdvanceAmount = TimeSpan.FromMilliseconds(1);

        await Delete().HandleAsync(budget.BusinessId, default);
        var row = await Restore().HandleAsync(budget.BusinessId, default);

        Assert.Equal(2, row.TransactionCount);
        Assert.Equal(500m, row.MonthlyLimit);
    }

    [Fact]
    public async Task Restore_does_not_silently_undo_an_earlier_reset()
    {
        // Dzieci skasowane resetem mają INNY znacznik czasu niż te skasowane razem z budżetem.
        // Bez porównania po czasie przywrócenie cofnęłoby także reset — a tego nikt nie prosił.
        var budget = await SeedBudgetAsync();

        await Reset().HandleAsync(budget.BusinessId, default);
        _clock.Advance(TimeSpan.FromHours(1));
        await Delete().HandleAsync(budget.BusinessId, default);

        var row = await Restore().HandleAsync(budget.BusinessId, default);

        Assert.Equal(BudgetStatus.Active, row.Status);
        Assert.Equal(0, row.TransactionCount);
    }

    // ── Sprzątanie po oknie retencji ───────────────────────────────────────────────────

    [Fact]
    public async Task Purge_removes_budgets_deleted_longer_ago_than_the_retention_window()
    {
        var stale = await SeedBudgetAsync("Stary");
        var fresh = await SeedBudgetAsync("Swiezy");

        await Delete().HandleAsync(stale.BusinessId, default);
        _clock.Advance(TimeSpan.FromDays(31));
        await Delete().HandleAsync(fresh.BusinessId, default);

        var purged = await Purge().HandleAsync(default);

        Assert.Equal(1, purged);

        // Stary znika naprawdę, razem z dziećmi — soft delete bez sprzątania to baza,
        // która nigdy nie maleje.
        var remaining = await _db.Budgets.IgnoreQueryFilters().Select(b => b.Name).ToListAsync();
        Assert.Equal(["Swiezy"], remaining);
        Assert.Equal(2, await _db.Transactions.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Purge_never_touches_a_live_budget()
    {
        await SeedBudgetAsync("Zywy");
        _clock.Advance(TimeSpan.FromDays(365));

        Assert.Equal(0, await Purge().HandleAsync(default));
        Assert.Single(await _db.Budgets.ToListAsync());
    }
}
