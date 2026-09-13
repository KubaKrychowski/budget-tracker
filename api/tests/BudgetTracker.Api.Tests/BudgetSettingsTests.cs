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
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
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
        new BudgetPurger(_db), Options.Create(_options), _clock,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PurgeDeletedBudgetsCommandHandler>.Instance);

    private ForcePurgeDeletedBudgetsCommandHandler ForcePurge() => new(
        new BudgetPurger(_db),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<ForcePurgeDeletedBudgetsCommandHandler>.Instance);

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

        // Cel i rezerwacja też są dziećmi budżetu — bez nich testy cyklu życia przechodziłyby
        // na zbiorze, w którym nie ma czego zgubić (a właśnie gubienie ich było błędem).
        _db.Add(new SavingsGoal(budget.BusinessId, 1500m, new DateOnly(2026, 9, 1), _clock.GetUtcNow()));
        _db.Add(new SavingsReservation(
            budget.BusinessId, $"Rezerwacja {name}", 800m, new DateOnly(2026, 10, 1), 1, _clock.GetUtcNow()));
        await _db.SaveChangesAsync();

        return budget;
    }

    /// <summary>Ile celów i rezerwacji budżetu widzi aplikacja (czyli po filtrze globalnym).</summary>
    private async Task<(int Goals, int Reservations)> LiveSavingsAsync(Budget budget) => (
        await _db.SavingsGoals.CountAsync(g => g.BudgetBusinessId == budget.BusinessId),
        await _db.SavingsReservations.CountAsync(r => r.BudgetBusinessId == budget.BusinessId));

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

    // ── Sprzątanie wymuszone (z pominięciem retencji) ──────────────────────────────────

    [Fact]
    public async Task Force_purge_removes_a_budget_deleted_a_moment_ago()
    {
        // Sedno tego zadania: budżet usunięty przed chwilą jest DALEKO wewnątrz okna retencji,
        // więc zwykłe sprzątanie go nie rusza — i to jest dowód, że wymuszenie faktycznie działa,
        // a nie że po prostu wywołaliśmy drugą drogę do tego samego progu.
        var budget = await SeedBudgetAsync("Dopiero usuniety");
        await Delete().HandleAsync(budget.BusinessId, default);

        Assert.Equal(0, await Purge().HandleAsync(default));

        Assert.Equal(1, await ForcePurge().HandleAsync(default));

        Assert.Empty(await _db.Budgets.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Force_purge_takes_the_children_with_it()
    {
        // Bez tego w bazie zostają transakcje bez budżetu — a to jest gorsze niż nieposprzątany
        // budżet, bo dashboard filtruje po BudgetBusinessId i takie wiersze stają się niewidoczne,
        // nie znikające. Przy danych, których nie wolno trzymać, „niewidoczne" nie wystarcza.
        var budget = await SeedBudgetAsync("Z dziecmi");
        await Delete().HandleAsync(budget.BusinessId, default);

        await ForcePurge().HandleAsync(default);

        Assert.Empty(await _db.Transactions.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await _db.ImportBatches.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await _db.BudgetItems.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Force_purge_never_touches_a_live_budget()
    {
        // ⚠️ Najważniejszy z tych testów. Wymuszenie pomija CZEKANIE, a nie krok „usuń budżet".
        // Gdyby warunek `DeletedAt != null` kiedyś wypadł z zapytania, to zadanie zamieniłoby się
        // w „skasuj wszystko" — bez komunikatu, bez potwierdzenia i bez możliwości cofnięcia.
        var live = await SeedBudgetAsync("Zywy");
        var deleted = await SeedBudgetAsync("Usuniety");
        await Delete().HandleAsync(deleted.BusinessId, default);

        Assert.Equal(1, await ForcePurge().HandleAsync(default));

        var remaining = await _db.Budgets.IgnoreQueryFilters().Select(b => b.Name).ToListAsync();
        Assert.Equal(["Zywy"], remaining);
        Assert.Equal(2, await _db.Transactions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(live.BusinessId, (await _db.Budgets.SingleAsync()).BusinessId);
    }

    // ── Cele oszczędzania i rezerwacje jako dzieci budżetu ─────────────────────────────

    [Fact]
    public async Task Deleting_a_budget_takes_its_goal_and_reservations_with_it()
    {
        // ⚠️ Wcześniej NIE zabierało. Cel zostawał żywy i wskazywał na budżet, którego nie widać,
        // bo zasięg ekranów oszczędności czyta tylko nieusunięte budżety.
        var budget = await SeedBudgetAsync();
        Assert.Equal((1, 1), await LiveSavingsAsync(budget));

        await Delete().HandleAsync(budget.BusinessId, default);

        Assert.Equal((0, 0), await LiveSavingsAsync(budget));
    }

    [Fact]
    public async Task Restoring_a_budget_brings_its_goal_and_reservations_back()
    {
        // Przywracanie rozpoznaje dzieci po RÓWNOŚCI znacznika usunięcia — cele i rezerwacje
        // muszą być stemplowane tym samym czasem co budżet, inaczej wracałby bez nich.
        var budget = await SeedBudgetAsync();
        await Delete().HandleAsync(budget.BusinessId, default);

        await Restore().HandleAsync(budget.BusinessId, default);

        Assert.Equal((1, 1), await LiveSavingsAsync(budget));
    }

    [Fact]
    public async Task Reset_leaves_the_goal_and_reservations_alone()
    {
        // ⚠️ Świadoma granica: cel i rezerwacja są ZASADĄ, nie danymi. Reset czyści to, co
        // wpadło do budżetu (transakcje, importy, limity), a nie to, co o nim postanowiono.
        var budget = await SeedBudgetAsync();

        await Reset().HandleAsync(budget.BusinessId, default);

        Assert.Equal((1, 1), await LiveSavingsAsync(budget));
        Assert.Empty(await _db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Purge_removes_the_goal_and_reservations_so_no_orphans_are_left()
    {
        // ⚠️ To jedyne miejsce kasujące fizycznie, więc sierota powstała tutaj zostaje na zawsze:
        // nieosiągalna z aplikacji i niesprzątana przez nic innego.
        var budget = await SeedBudgetAsync();
        await Delete().HandleAsync(budget.BusinessId, default);
        _clock.Advance(TimeSpan.FromDays(31));

        Assert.Equal(1, await Purge().HandleAsync(default));

        Assert.Empty(await _db.SavingsGoals.IgnoreQueryFilters()
            .Where(g => g.BudgetBusinessId == budget.BusinessId).ToListAsync());
        Assert.Empty(await _db.SavingsReservations.IgnoreQueryFilters()
            .Where(r => r.BudgetBusinessId == budget.BusinessId).ToListAsync());
    }

    [Fact]
    public async Task Force_purge_removes_the_goal_and_reservations_too()
    {
        var budget = await SeedBudgetAsync();
        await Delete().HandleAsync(budget.BusinessId, default);

        Assert.Equal(1, await ForcePurge().HandleAsync(default));

        Assert.Empty(await _db.SavingsGoals.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await _db.SavingsReservations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Deleting_one_budget_does_not_touch_another_budgets_savings()
    {
        // Najważniejszy z tej piątki: „przypisane per budżet" znaczy też, że kasowanie jednego
        // nie ma prawa ruszyć drugiego. Oba mają cel o tej samej kwocie i ten sam priorytet
        // rezerwacji, więc rozróżnia je WYŁĄCZNIE BudgetBusinessId.
        var mine = await SeedBudgetAsync("Moj");
        var other = await SeedBudgetAsync("Obcy");

        await Delete().HandleAsync(mine.BusinessId, default);

        Assert.Equal((0, 0), await LiveSavingsAsync(mine));
        Assert.Equal((1, 1), await LiveSavingsAsync(other));
    }

    [Fact]
    public async Task Force_purge_on_a_clean_database_does_nothing()
    {
        // Idempotencja: Hangfire ponawia zadanie po błędzie, a przycisk „Trigger now" da się
        // kliknąć dwa razy. Drugi przebieg ma zwrócić zero, nie wywalić się na pustym zbiorze.
        await SeedBudgetAsync("Zywy");

        Assert.Equal(0, await ForcePurge().HandleAsync(default));
        Assert.Equal(0, await ForcePurge().HandleAsync(default));
        Assert.Single(await _db.Budgets.ToListAsync());
    }
}
