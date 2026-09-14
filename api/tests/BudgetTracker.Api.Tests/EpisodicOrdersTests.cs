using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.EpisodicOrders.Commands;
using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Exceptions;
using BudgetTracker.Api.Features.EpisodicOrders.Queries;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Features.Savings.Commands;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.Savings.Queries;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Queries;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Zlecenia epizodyczne: plan albo transakcja jako jedno źródło kwoty, rezerwacja rządzona przez zlecenie, kupienie
/// i cofnięcie, kandydaci, cykl życia budżetu i lista transakcji.
///
/// Opisy i kwoty są zmyślone. Wymaga `docker compose up -d db`.
/// </summary>
public sealed class EpisodicOrdersTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_episodic_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś” to 14 września 2026.</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly November = new(2026, 11, 1);

    private AppDbContext _db = null!;
    private Budget _budget = null!;
    private Category _electronics = null!;
    private Category _savings = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _electronics = new Category("Elektronika");
        _savings = new Category("Oszczędności");
        _budget = new Budget("Domowy", new DateOnly(2026, 1, 1), 0m, _clock.GetUtcNow());
        _db.AddRange(_electronics, _savings, _budget);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private EpisodicOrdersBudgetScope Scope() => new(_db, _clock);

    private EpisodicOrderLookup Lookup() => new(_db);

    private EpisodicOrderTransactions Transactions() => new(_db);

    private CreateEpisodicOrderCommandHandler Create() => new(_db, Scope(), new EpisodicOrderRequestValidator(_db), Transactions());

    private UpdateEpisodicOrderCommandHandler Update() => new(_db, Lookup(), new EpisodicOrderRequestValidator(_db));

    private CreateEpisodicOrderReservationCommandHandler Reserve() => new(_db, Lookup(), Scope());

    private PurchaseEpisodicOrderCommandHandler Purchase() => new(_db, Lookup(), Transactions(), Scope());

    private GetEpisodicOrdersQueryHandler Query() =>
        new(_db, Scope(), new GetSavingsReservationsQueryHandler(_db, new SavingsBudgetScope(_db, _clock), new SavingsAccount(_db, new SavingsCategory(_db))));

    private ContributeToReservationCommandHandler Contribute() =>
        new(_db, new ReservationLookup(_db), new SavingsAccount(_db, new SavingsCategory(_db)), new SavingsBudgetScope(_db, _clock));

    private GetEpisodicOrderCandidatesQueryHandler Candidates() => new(_db, Scope(), Lookup(), Transactions());

    private SaveEpisodicOrderRequestDto Laptop(decimal amount = 4000m, DateOnly? due = null) =>
        new(_budget.BusinessId, "Nowy laptop", "do pracy", null, _electronics.BusinessId, amount, due ?? November);

    private Transaction Add(DateOnly date, decimal amount, string description, Category? category = null, Guid? budget = null)
    {
        var t = new Transaction(date, amount, description, _clock.GetUtcNow(), TransactionStatus.Confirmed,
            categoryId: (category ?? _electronics).Id, budgetBusinessId: budget ?? _budget.BusinessId);
        _db.Transactions.Add(t);
        return t;
    }

    private async Task<EpisodicOrdersResponseDto> ScreenAsync()
    {
        _db.ChangeTracker.Clear();
        return await Query().HandleAsync(_budget.BusinessId, default);
    }

    // ── Zapis ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Zaplanowane_ma_plan_a_zrealizowane_bierze_kwote_date_i_kategorie_z_transakcji()
    {
        var bought = Add(new DateOnly(2026, 8, 20), -1200m, "WARSZTAT");
        await _db.SaveChangesAsync();

        await Create().HandleAsync(Laptop(), default);
        await Create().HandleAsync(
            new SaveEpisodicOrderRequestDto(_budget.BusinessId, "Serwis auta", null, bought.BusinessId, null, null, null), default);

        var screen = await ScreenAsync();
        var planned = Assert.Single(screen.Planned);
        var realized = Assert.Single(screen.Realized);

        Assert.Equal((4000m, November, "Elektronika"), (planned.Amount, planned.DueMonth!.Value, planned.CategoryName!));
        Assert.Equal((1200m, new DateOnly(2026, 8, 20), "WARSZTAT"), (realized.Amount, realized.Date!.Value, realized.TransactionDescription!));
        Assert.False(realized.WasPlanned);
        Assert.Equal(4000m, screen.PlannedTotal);
        Assert.Equal(1200m, screen.RealizedThisYear);
        Assert.Equal(1, screen.WithoutSavingsCount);
    }

    [Fact]
    public async Task Zakup_bez_terminu_jest_na_koncu_listy_a_jego_cel_to_rezerwacja_przy_okazji()
    {
        Add(new DateOnly(2026, 8, 5), -3000m, "PRZELEW NA OSZCZEDNOSCI", _savings);
        await _db.SaveChangesAsync();
        var someday = await Create().HandleAsync(Laptop(amount: 2000m) with { Name = "Rower", DueMonth = null }, default);
        var dated = await Create().HandleAsync(Laptop(amount: 2500m), default);

        await Reserve().HandleAsync(someday.Id, default);
        await Reserve().HandleAsync(dated.Id, default);

        var screen = await ScreenAsync();
        Assert.Equal(["Nowy laptop", "Rower"], screen.Planned.Select(r => r.Name));
        Assert.Null(screen.Planned[1].DueMonth);
        // Założenie celu niczego nie wpłaca (#23) — uzbierane rośnie dopiero z wpłatami.
        Assert.Equal((0m, 0m), (screen.Planned[0].Collected!.Value, screen.Planned[1].Collected!.Value));
        Assert.Null((await _db.SavingsReservations.AsNoTracking().SingleAsync(r => r.Name == "Rower")).DueMonth);
    }

    [Fact]
    public async Task Walidacja_nazwy_i_planu()
    {
        await Assert.ThrowsAsync<EpisodicOrderNameRequiredException>(
            () => Create().HandleAsync(Laptop() with { Name = "  " }, default));
        await Assert.ThrowsAsync<EpisodicOrderPlanInvalidException>(
            () => Create().HandleAsync(Laptop(amount: 0m), default));
        await Assert.ThrowsAsync<EpisodicOrderPlanInvalidException>(
            () => Create().HandleAsync(Laptop() with { CategoryId = Guid.NewGuid() }, default));
    }

    [Fact]
    public async Task Transakcja_musi_byc_wydatkiem_z_tego_budzetu_i_nalezy_do_jednego_zlecenia()
    {
        var income = Add(new DateOnly(2026, 8, 1), 500m, "ZWROT");
        var expense = Add(new DateOnly(2026, 8, 2), -500m, "SKLEP");
        var other = new Budget("Wariant", new DateOnly(2026, 1, 1), 0m, _clock.GetUtcNow());
        _db.Budgets.Add(other);
        await _db.SaveChangesAsync();
        var foreign = Add(new DateOnly(2026, 8, 3), -500m, "SKLEP", budget: other.BusinessId);
        await _db.SaveChangesAsync();

        SaveEpisodicOrderRequestDto Realized(Guid id) => new(_budget.BusinessId, "Zakup", null, id, null, null, null);

        await Assert.ThrowsAsync<EpisodicOrderTransactionInvalidException>(() => Create().HandleAsync(Realized(income.BusinessId), default));
        await Assert.ThrowsAsync<EpisodicOrderTransactionInvalidException>(() => Create().HandleAsync(Realized(foreign.BusinessId), default));
        await Create().HandleAsync(Realized(expense.BusinessId), default);
        await Assert.ThrowsAsync<EpisodicOrderTransactionInvalidException>(() => Create().HandleAsync(Realized(expense.BusinessId), default));

        // Z listy transakcji budżet nie przychodzi — zlecenie idzie do budżetu swojej transakcji, nie do domyślnego.
        await Create().HandleAsync(new SaveEpisodicOrderRequestDto(null, "Zakup", null, foreign.BusinessId, null, null, null), default);
        Assert.Equal(other.BusinessId, (await _db.EpisodicOrders.SingleAsync(o => o.TransactionBusinessId == foreign.BusinessId)).BudgetBusinessId);
    }

    // ── Rezerwacja i kupienie ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Zaloz_cel_tworzy_rezerwacje_ze_zlecenia_a_zmiana_planu_ja_przepisuje()
    {
        Add(new DateOnly(2026, 8, 5), -1800m, "PRZELEW NA OSZCZEDNOSCI", _savings);
        await _db.SaveChangesAsync();
        var order = await Create().HandleAsync(Laptop(), default);

        await Reserve().HandleAsync(order.Id, default);
        await Assert.ThrowsAsync<EpisodicOrderStateConflictException>(() => Reserve().HandleAsync(order.Id, default));

        _db.ChangeTracker.Clear();
        await Update().HandleAsync(order.Id, Laptop(amount: 3500m, due: new DateOnly(2026, 12, 15)) with { Name = "Laptop" }, default);

        var reservation = await _db.SavingsReservations.AsNoTracking().SingleAsync();
        Assert.Equal(("Laptop", 3500m, new DateOnly(2026, 12, 1)), (reservation.Name, reservation.Amount, reservation.DueMonth));

        var reservationId = (await _db.SavingsReservations.AsNoTracking().SingleAsync()).BusinessId;
        await Contribute().ContributeAsync(reservationId, new ContributeRequestDto(1500m), default);

        var screen = await ScreenAsync();
        var row = Assert.Single(screen.Planned);
        Assert.Equal(1500m, row.Collected);
        Assert.Equal((1500m, 3500m, 0), (screen.CollectedTotal, screen.ReservedTotal, screen.WithoutSavingsCount));

        // Planu nie da się obniżyć poniżej tego, co już wpłacono na jego rezerwację.
        _db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ReservationAmountBelowContributedException>(
            () => Update().HandleAsync(order.Id, Laptop(amount: 1000m), default));
    }

    [Fact]
    public async Task Kupione_rozlicza_rezerwacje_a_cofniecie_przywraca_plan_i_zbieranie()
    {
        var order = await Create().HandleAsync(Laptop(), default);
        await Reserve().HandleAsync(order.Id, default);
        var bought = Add(new DateOnly(2026, 10, 30), -4200m, "SKLEP ELEKTRONICZNY");
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await Purchase().PurchaseAsync(order.Id, new PurchaseEpisodicOrderRequestDto(bought.BusinessId), default);

        Assert.Equal(bought.BusinessId, (await _db.SavingsReservations.AsNoTracking().SingleAsync()).SettledTransactionBusinessId);
        var realized = Assert.Single((await ScreenAsync()).Realized);
        Assert.Equal(4200m, realized.Amount);
        Assert.True(realized.WasPlanned);
        await Assert.ThrowsAsync<EpisodicOrderStateConflictException>(
            () => Purchase().PurchaseAsync(order.Id, new PurchaseEpisodicOrderRequestDto(bought.BusinessId), default));

        _db.ChangeTracker.Clear();
        await Purchase().UndoAsync(order.Id, default);

        Assert.Null((await _db.SavingsReservations.AsNoTracking().SingleAsync()).SettledAt);
        var planned = Assert.Single((await ScreenAsync()).Planned);
        Assert.Equal(4000m, planned.Amount);
    }

    [Fact]
    public async Task Zlecenia_bez_planu_nie_da_sie_cofnac_ani_zalozyc_mu_celu()
    {
        var bought = Add(new DateOnly(2026, 8, 20), -300m, "OPLATA");
        await _db.SaveChangesAsync();
        var order = await Create().HandleAsync(
            new SaveEpisodicOrderRequestDto(null, "Mandat", null, bought.BusinessId, null, null, null), default);

        await Assert.ThrowsAsync<EpisodicOrderStateConflictException>(() => Purchase().UndoAsync(order.Id, default));
        await Assert.ThrowsAsync<EpisodicOrderStateConflictException>(() => Reserve().HandleAsync(order.Id, default));
    }

    [Fact]
    public async Task Usuniecie_zaplanowanego_zabiera_nierozliczona_rezerwacje_a_transakcja_zostaje()
    {
        var order = await Create().HandleAsync(Laptop(), default);
        await Reserve().HandleAsync(order.Id, default);
        _db.ChangeTracker.Clear();

        await new DeleteEpisodicOrderCommandHandler(_db, Lookup()).HandleAsync(order.Id, default);

        Assert.Empty(await _db.SavingsReservations.ToListAsync());
        Assert.Empty(await _db.EpisodicOrders.ToListAsync());
        await Assert.ThrowsAsync<EpisodicOrderNotFoundException>(() => Reserve().HandleAsync(order.Id, default));
    }

    // ── Kandydaci ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Kandydaci_od_dwoch_miesiecy_przed_terminem_bez_zajetych_i_z_fraza_jako_danymi()
    {
        var tooEarly = Add(new DateOnly(2026, 8, 31), -100m, "SKLEP A");
        var inWindow = Add(new DateOnly(2026, 9, 1), -100m, "SKLEP 50% RABAT");
        var other = Add(new DateOnly(2026, 9, 2), -100m, "SKLEP 500 PLN");
        var taken = Add(new DateOnly(2026, 10, 1), -100m, "SKLEP C");
        await _db.SaveChangesAsync();
        var order = await Create().HandleAsync(Laptop(), default);
        await Create().HandleAsync(new SaveEpisodicOrderRequestDto(null, "Inne", null, taken.BusinessId, null, null, null), default);

        var all = await Candidates().HandleAsync(null, order.Id, null, default);
        var searched = await Candidates().HandleAsync(null, order.Id, "50%", default);

        Assert.Equal([other.BusinessId, inWindow.BusinessId], all.Select(c => c.Id));
        Assert.DoesNotContain(all, c => c.Id == tooEarly.BusinessId);
        Assert.Equal(inWindow.BusinessId, Assert.Single(searched).Id);
    }

    // ── Cykl życia i widoki poboczne ─────────────────────────────────────────────────────

    [Fact]
    public async Task Znikniecie_transakcji_ukrywa_zrealizowane_a_zaplanowane_wraca_do_planu()
    {
        var bought = Add(new DateOnly(2026, 8, 20), -300m, "OPLATA");
        var planBought = Add(new DateOnly(2026, 10, 20), -4000m, "SKLEP");
        await _db.SaveChangesAsync();
        await Create().HandleAsync(new SaveEpisodicOrderRequestDto(null, "Mandat", null, bought.BusinessId, null, null, null), default);
        var laptop = await Create().HandleAsync(Laptop(), default);
        await Purchase().PurchaseAsync(laptop.Id, new PurchaseEpisodicOrderRequestDto(planBought.BusinessId), default);

        _db.Transactions.RemoveRange(bought, planBought);
        await _db.SaveChangesAsync();

        var screen = await ScreenAsync();
        Assert.Empty(screen.Realized);
        Assert.Equal("Nowy laptop", Assert.Single(screen.Planned).Name);
    }

    [Fact]
    public async Task Usuniecie_budzetu_zabiera_zlecenia_przywrocenie_oddaje_reset_zostawia_purge_kasuje()
    {
        await Create().HandleAsync(Laptop(), default);
        var children = new BudgetChildren(_db);
        var at = _clock.GetUtcNow();

        await children.SoftDeleteAsync(_budget, at, default);
        Assert.Single(await _db.EpisodicOrders.ToListAsync());

        await children.SoftDeleteSavingsAsync(_budget, at, default);
        Assert.Empty(await _db.EpisodicOrders.ToListAsync());

        await children.RestoreAsync(_budget, at, default);
        Assert.Single(await _db.EpisodicOrders.ToListAsync());

        await children.SoftDeleteSavingsAsync(_budget, at, default);
        _budget.MarkDeleted(at);
        await _db.SaveChangesAsync();
        await new BudgetPurger(_db).PurgeAsync(null, default);
        Assert.Empty(await _db.EpisodicOrders.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Lista_transakcji_podaje_nazwe_zlecenia_a_ekran_celow_liczy_je_jako_jednorazowe()
    {
        var bought = Add(new DateOnly(2026, 8, 20), -1200m, "WARSZTAT");
        Add(new DateOnly(2026, 8, 21), -50m, "KAWA");
        await _db.SaveChangesAsync();
        await Create().HandleAsync(new SaveEpisodicOrderRequestDto(null, "Serwis auta", null, bought.BusinessId, null, null, null), default);
        _db.ChangeTracker.Clear();

        var list = await new GetTransactionsListQueryHandler(
                new TransactionBudgetScope(_db, _clock), new TransactionFilters(_db), new TransactionListItemReader(_db))
            .HandleAsync(new TransactionFilterRequestDto([_budget.BusinessId], null, null, null, false, TransactionDirection.All,
                null, null, null, null), 1, 20, null, true, default);
        var savings = await new GetSavingsQueryHandler(_db, new SavingsBudgetScope(_db, _clock), new SavingsCategory(_db))
            .HandleAsync([_budget.BusinessId], default);

        Assert.Equal("Serwis auta", list.Items.Single(i => i.Description == "WARSZTAT").EpisodicOrderName);
        Assert.Null(list.Items.Single(i => i.Description == "KAWA").EpisodicOrderName);
        Assert.True(savings.HasAnyEpisodicExpense);
        Assert.Equal(1200m, savings.Months.Single(m => m.Month == new DateOnly(2026, 8, 1)).OneOffTotal);
    }
}
