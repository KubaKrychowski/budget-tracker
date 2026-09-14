using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Savings.Commands;
using BudgetTracker.Api.Features.Savings.Consts;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.Savings.Queries;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Rezerwacje na koncie oszczędnościowym.
///
/// Te testy pilnują reguł z zgłoszenia #23, których nie widać z ekranu: uzbierane to suma WPŁAT (nie rozkład stanu
/// konta), wpłaty mają dwa limity (brakująca kwota i stan konta), a rozliczona rezerwacja przestaje pomniejszać
/// wolne środki.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class SavingsReservationTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_reservations_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś" to 15 listopada 2026 — po części terminów, przed resztą.</summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));

    private AppDbContext _db = null!;
    private Guid _budgetId;
    private int _savingsCategoryId;
    private int _foodCategoryId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        // Nazwa MUSI się zgadzać z `SavingsCategory.Name` — to jest dziś
        // jedyny sygnał stanu konta oszczędnościowego (fallback do czasu #10).
        var savings = new Category("Oszczędności");
        var food = new Category("Jedzenie");
        _db.Categories.AddRange(savings, food);

        var budget = new Budget("Podstawowy", new DateOnly(2026, 1, 1), 0m, default);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        _savingsCategoryId = savings.Id;
        _foodCategoryId = food.Id;
        _budgetId = budget.BusinessId;

    }

    private SavingsBudgetScope Scope() => new(_db, _clock);

    private ReservationLookup Lookup() => new(_db);

    private GetSavingsReservationsQueryHandler GetHandler() => new(_db, Scope(), Account());

    private SavingsAccount Account() => new(_db, new SavingsCategory(_db));

    private ContributeToReservationCommandHandler ContributeHandler() => new(_db, Lookup(), Account(), Scope());

    private UpdateSavingsReservationCommandHandler UpdateHandler() => new(_db, Lookup(), Scope());

    private GetSettleCandidatesQueryHandler CandidatesHandler() => new(_db, Lookup(), new SavingsCategory(_db));

    private CreateSavingsReservationCommandHandler CreateHandler() => new(_db, Scope(), _clock);

    private DeleteSavingsReservationCommandHandler DeleteHandler() => new(_db, Lookup());

    private SettleSavingsReservationCommandHandler SettleHandler() =>
        new(_db, Lookup(), new SavingsCategory(_db), Scope(), _clock);

    private UnsettleSavingsReservationCommandHandler UnsettleHandler() => new(_db, Lookup(), Scope());

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    // ── Przełącznik budżetu ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Response_carries_budget_options_even_without_reservations()
    {
        // Pusta lista rezerwacji to właśnie ten moment, w którym ktoś szuka innego budżetu.
        var other = new Budget("Wariant", new DateOnly(2025, 6, 1), 0m, default);
        _db.Budgets.Add(other);
        await _db.SaveChangesAsync();

        var response = await GetHandler().HandleAsync(null, default);

        Assert.Empty(response.Reservations);
        Assert.Equal([_budgetId, other.BusinessId], response.Budgets.Select(b => b.Id));
        Assert.Equal(_budgetId, Assert.Single(response.SelectedBudgetIds));
    }

    // ── Wpłaty na cel ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nowa_rezerwacja_przy_pelnym_koncie_nie_jest_uzbierana()
    {
        // Sedno #23: dawna kolejka pokazywała 100% zaraz po założeniu, bo rozkładała stan konta sama.
        Deposit(2026, 10, 5000m);
        await _db.SaveChangesAsync();
        await Reserve("Aparat", 800m, new DateOnly(2027, 3, 1));

        var response = await GetHandler().HandleAsync([_budgetId], default);

        Assert.Equal(0m, response.Reservations.Single().Collected);
        Assert.Equal((0m, 5000m), (response.CollectedTotal, response.AvailableToContribute));
    }

    [Fact]
    public async Task Wplata_zwieksza_uzbierane_a_wycofanie_konkretnej_wplaty_je_zmniejsza()
    {
        Deposit(2026, 10, 2000m);
        await _db.SaveChangesAsync();
        var reservation = await Reserve("Rower", 2000m, null);

        await ContributeHandler().ContributeAsync(reservation.Id, new ContributeRequestDto(400m), default);
        var view = await ContributeHandler().ContributeAsync(reservation.Id, new ContributeRequestDto(600m), default);
        Assert.Equal(1000m, view.Collected);
        Assert.Equal(2, view.Contributions.Count);

        var withdrawn = await ContributeHandler().WithdrawAsync(
            reservation.Id, view.Contributions.Single(c => c.Amount == 400m).Id, default);

        Assert.Equal(600m, withdrawn.Collected);
        var response = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal((600m, 1400m), (response.CollectedTotal, response.AvailableToContribute));
    }

    [Fact]
    public async Task Nie_da_sie_wplacic_wiecej_niz_brakuje_do_celu_ani_wiecej_niz_zostalo_na_koncie()
    {
        Deposit(2026, 10, 1000m);
        await _db.SaveChangesAsync();
        var small = await Reserve("Kurs", 400m, new DateOnly(2027, 1, 1));
        var big = await Reserve("Aparat", 1600m, new DateOnly(2027, 3, 1));

        await ContributeHandler().ContributeAsync(small.Id, new ContributeRequestDto(300m), default);
        await Assert.ThrowsAsync<ContributionAmountInvalidException>(
            () => ContributeHandler().ContributeAsync(small.Id, new ContributeRequestDto(101m), default));
        await Assert.ThrowsAsync<ContributionAmountInvalidException>(
            () => ContributeHandler().ContributeAsync(small.Id, new ContributeRequestDto(0m), default));

        // Na koncie 1000, na „Kurs” wpłacone 300 — na „Aparat” zostaje 700, choć brakuje mu 1600.
        await Assert.ThrowsAsync<ContributionExceedsBalanceException>(
            () => ContributeHandler().ContributeAsync(big.Id, new ContributeRequestDto(701m), default));
        await ContributeHandler().ContributeAsync(big.Id, new ContributeRequestDto(700m), default);
    }

    [Fact]
    public async Task Rozliczona_rezerwacja_nie_przyjmuje_wplat_a_wplaty_na_nia_zwalniaja_miejsce_na_koncie()
    {
        Deposit(2026, 9, 3000m);
        var withdrawal = Withdraw(2026, 10, 1000m);
        await _db.SaveChangesAsync();
        var reservation = await Reserve("Ubezpieczenie", 1000m, new DateOnly(2026, 10, 1));
        await ContributeHandler().ContributeAsync(reservation.Id, new ContributeRequestDto(1000m), default);

        await SettleHandler().HandleAsync(reservation.Id, new SettleReservationRequestDto(withdrawal), default);

        await Assert.ThrowsAsync<ReservationAlreadySettledException>(
            () => ContributeHandler().ContributeAsync(reservation.Id, new ContributeRequestDto(1m), default));
        // Wypłata zeszła z konta (2000 zostało), a wpłaty rozliczonej już się nie liczą.
        var response = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal((2000m, 0m, 2000m), (response.AccountBalance, response.CollectedTotal, response.AvailableToContribute));
    }

    [Fact]
    public async Task Kwota_rezerwacji_nie_spada_ponizej_wplat_a_nieznana_wplata_to_404()
    {
        Deposit(2026, 10, 1000m);
        await _db.SaveChangesAsync();
        var reservation = await Reserve("Aparat", 800m, new DateOnly(2027, 3, 1));
        await ContributeHandler().ContributeAsync(reservation.Id, new ContributeRequestDto(500m), default);

        await Assert.ThrowsAsync<ReservationAmountBelowContributedException>(() => UpdateHandler().HandleAsync(
            reservation.Id, new SaveReservationRequestDto("Aparat", 499m, new DateOnly(2027, 3, 1)), default));
        await Assert.ThrowsAsync<ContributionNotFoundException>(
            () => ContributeHandler().WithdrawAsync(reservation.Id, Guid.NewGuid(), default));
    }

    // ── Wolne środki ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rozliczenie_zwalnia_wolne_srodki()
    {
        // Zgłoszenie #23, decyzja użytkownika: zapłacony zakup zszedł już ze stanu konta, więc jego rezerwacja
        // przestaje pomniejszać wolne środki. Dawniej koperta była roczna i odejmowała się także po rozliczeniu.
        Deposit(2026, 9, 3000m);
        var withdrawal = Withdraw(2026, 10, 1800m);
        await _db.SaveChangesAsync();

        var reservation = await Reserve("Ubezpieczenie", 1800m, new DateOnly(2026, 5, 1));

        var before = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(1200m, before.AccountBalance);
        Assert.Equal(-600m, before.FreeFunds);

        await SettleHandler().HandleAsync(reservation.Id, new SettleReservationRequestDto(withdrawal), default);

        var after = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(1200m, after.FreeFunds);
        Assert.Equal((1800m, 0m), (after.SettledTotal, after.ReservedTotal));
        Assert.Equal(ReservationStatus.Settled, after.Reservations.Single().Status);
    }

    [Fact]
    public async Task Free_funds_subtract_the_full_amount_of_open_reservations_and_may_go_negative()
    {
        Deposit(2026, 10, 1000m);
        await _db.SaveChangesAsync();
        var reservation = await Reserve("Aparat", 1600m, new DateOnly(2027, 3, 1));
        await ContributeHandler().ContributeAsync(reservation.Id, new ContributeRequestDto(1000m), default);

        var response = await GetHandler().HandleAsync([_budgetId], default);

        // Wolne środki odejmują PEŁNĄ kwotę rezerwacji (decyzja z #23), nie wpłaty. Ujemne to poprawna informacja —
        // front pokazuje je jako osobny stan („rezerwacje przekraczają stan konta o X"), a nie minus w kaflu.
        Assert.Equal(-600m, response.FreeFunds);
        Assert.Equal((1000m, 0m), (response.CollectedTotal, response.AvailableToContribute));
    }

    [Fact]
    public async Task Covered_by_uses_the_monthly_goal_and_is_null_without_one()
    {
        Deposit(2026, 10, 1000m);
        await Reserve("Aparat", 2500m, new DateOnly(2027, 6, 1));

        var withoutGoal = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Null(withoutGoal.CoveredBy);

        _db.SavingsGoals.Add(new SavingsGoal(_budgetId, 500m, new DateOnly(2026, 1, 1), _clock.GetUtcNow()));
        await _db.SaveChangesAsync();

        // Nic nie wpłacono, brakuje 2 500 zł, tempo 500 zł/mies. → pięć miesięcy od listopada, czyli kwiecień.
        var withGoal = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(new DateOnly(2027, 4, 1), withGoal.CoveredBy);
    }

    // ── Statusy ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Status_is_overdue_only_when_the_due_month_passed_without_settlement()
    {
        Deposit(2026, 10, 5000m);

        await Reserve("Po terminie", 400m, new DateOnly(2026, 3, 1));
        await Reserve("Zbiera", 700m, new DateOnly(2027, 6, 1));
        var settled = await Reserve("Rozliczona", 300m, new DateOnly(2026, 2, 1));

        var withdrawal = Withdraw(2026, 4, 300m);
        await _db.SaveChangesAsync();
        await SettleHandler().HandleAsync(settled.Id, new SettleReservationRequestDto(withdrawal), default);

        var byName = (await GetHandler().HandleAsync([_budgetId], default))
            .Reservations.ToDictionary(r => r.Name);

        Assert.Equal(ReservationStatus.Overdue, byName["Po terminie"].Status);
        Assert.Equal(ReservationStatus.Collecting, byName["Zbiera"].Status);
        // Rozliczenie jest NADRZĘDNE nad terminem — inaczej zapłacone OC z maja wisiałoby
        // do końca świata jako „po terminie".
        Assert.Equal(ReservationStatus.Settled, byName["Rozliczona"].Status);
    }

    // ── Walidacja rozliczenia ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Settlement_rejects_a_transaction_that_is_not_a_savings_withdrawal()
    {
        var reservation = await Reserve("Ubezpieczenie", 1800m, new DateOnly(2026, 5, 1));

        // Wydatek na jedzenie: zła kategoria I zły znak. Bez tej walidacji „rozliczenie"
        // zamykałoby rezerwację dowolnym wierszem z wyciągu.
        var groceries = Add(2026, 10, -1800m, _foodCategoryId);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<SettlementTransactionInvalidException>(() =>
            SettleHandler().HandleAsync(reservation.Id, new SettleReservationRequestDto(groceries), default));
    }

    [Fact]
    public async Task Settlement_rejects_a_deposit_into_savings()
    {
        var reservation = await Reserve("Ubezpieczenie", 1800m, new DateOnly(2026, 5, 1));

        // Kategoria się zgadza, ale to WPŁATA (kwota ujemna). Rozliczenie wpłatą znaczyłoby
        // „zapłaciłem OC, odkładając pieniądze" — czyli nic.
        var deposit = Add(2026, 10, -1800m, _savingsCategoryId);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<SettlementTransactionInvalidException>(() =>
            SettleHandler().HandleAsync(reservation.Id, new SettleReservationRequestDto(deposit), default));
    }

    [Fact]
    public async Task One_withdrawal_cannot_settle_two_reservations()
    {
        var first = await Reserve("Ubezpieczenie", 1800m, new DateOnly(2026, 5, 1));
        var second = await Reserve("Przegląd", 1800m, new DateOnly(2026, 7, 1));

        var withdrawal = Withdraw(2026, 8, 1800m);
        await _db.SaveChangesAsync();

        await SettleHandler().HandleAsync(first.Id, new SettleReservationRequestDto(withdrawal), default);

        // Ta sama wypłata zamykająca dwie koperty zawyżyłaby „rozliczone" o 1 800 zł
        // i nikt by tego nie zauważył.
        await Assert.ThrowsAsync<SettlementTransactionInvalidException>(() =>
            SettleHandler().HandleAsync(second.Id, new SettleReservationRequestDto(withdrawal), default));
    }

    [Fact]
    public async Task Unsettling_frees_the_transaction_for_another_reservation()
    {
        var first = await Reserve("Ubezpieczenie", 1800m, new DateOnly(2026, 5, 1));
        var second = await Reserve("Przegląd", 1800m, new DateOnly(2026, 7, 1));

        var withdrawal = Withdraw(2026, 8, 1800m);
        await _db.SaveChangesAsync();

        await SettleHandler().HandleAsync(first.Id, new SettleReservationRequestDto(withdrawal), default);
        await UnsettleHandler().HandleAsync(first.Id, default);

        // Bez cofania rozliczenia wskazanie złej rezerwacji byłoby drzwiami w jedną stronę.
        var view = await SettleHandler().HandleAsync(
            second.Id, new SettleReservationRequestDto(withdrawal), default);

        Assert.Equal(ReservationStatus.Settled, view.Status);
    }

    // ── Podpowiedź rozliczenia ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Settle_candidate_from_a_later_month_is_suggested_first()
    {
        // Przypadek WPROST Z MAKIETY (170:1727): wypłata z 14.08.2026 dla rezerwacji z terminem
        // maj 2026. Rachunki płaci się po terminie, więc twardy filtr miesiąca nigdy by tego
        // kandydata nie pokazał — miesiąc jest kryterium KOLEJNOŚCI, nie widoczności.
        var reservation = await Reserve("Ubezpieczenie OC", 1800m, new DateOnly(2026, 5, 1));

        Withdraw(2026, 5, 250m);
        Withdraw(2026, 8, 1800m);
        await _db.SaveChangesAsync();

        var candidates = await CandidatesHandler().HandleAsync(reservation.Id, default);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(1800m, candidates[0].Amount);
        Assert.Equal(new DateOnly(2026, 8, 14), candidates[0].Date);
    }

    [Fact]
    public async Task Settle_candidates_skip_transactions_already_used_elsewhere()
    {
        var first = await Reserve("Ubezpieczenie", 1800m, new DateOnly(2026, 5, 1));
        var second = await Reserve("Przegląd", 1800m, new DateOnly(2026, 7, 1));

        var used = Withdraw(2026, 8, 1800m);
        Withdraw(2026, 9, 1800m);
        await _db.SaveChangesAsync();

        await SettleHandler().HandleAsync(first.Id, new SettleReservationRequestDto(used), default);

        var candidates = await CandidatesHandler().HandleAsync(second.Id, default);

        Assert.Single(candidates);
        Assert.NotEqual(used, candidates[0].Id);
    }

    // ── Walidacja zapisu ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reservation_without_a_name_is_rejected(string name)
    {
        await Assert.ThrowsAsync<ReservationNameRequiredException>(() => CreateHandler().HandleAsync(
            new SaveReservationRequestDto(name, 100m, new DateOnly(2027, 1, 1), 0, _budgetId), default));
    }

    [Fact]
    public async Task Reservation_amount_must_be_positive()
    {
        await Assert.ThrowsAsync<ReservationAmountInvalidException>(() => CreateHandler().HandleAsync(
            new SaveReservationRequestDto("Aparat", 0m, new DateOnly(2027, 1, 1), 0, _budgetId), default));
    }

    [Fact]
    public async Task Due_month_is_normalised_to_the_first_day()
    {
        var view = await CreateHandler().HandleAsync(
            new SaveReservationRequestDto("Aparat", 100m, new DateOnly(2027, 1, 23), 0, _budgetId),
            default);

        // Termin jest MIESIĘCZNY — dzień w nim nic nie znaczy, a przechowywany rozjeżdżałby
        // porównania „po terminie" o kilka tygodni.
        Assert.Equal(new DateOnly(2027, 1, 1), view.DueMonth);
    }

    [Fact]
    public async Task Deleted_reservation_stops_counting_against_free_funds()
    {
        Deposit(2026, 10, 1000m);
        var reservation = await Reserve("Aparat", 400m, new DateOnly(2027, 3, 1));

        await DeleteHandler().HandleAsync(reservation.Id, default);

        // Soft delete: wiersz zostaje w bazie, ale filtr globalny go nie widzi — więc wolne środki wracają.
        var response = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Empty(response.Reservations);
        Assert.Equal(1000m, response.FreeFunds);
    }

    // ── Kontrakt z frontem ───────────────────────────────────────────────────────────────

    [Fact]
    public void Status_jedzie_do_frontu_jako_NAZWA_a_nie_liczba()
    {
        // ⚠️ Ta sama regresja, którą złapaliśmy na żywym API przy `MonthVerdict`. Aplikacja nie
        // rejestruje globalnego `JsonStringEnumConverter`, więc bez atrybutu na TYPIE status
        // szedłby jako `0..2`, a front porównuje go z nazwą — każdy wiersz wpadłby w gałąź
        // domyślną, bez jednego błędu w konsoli. Testy frontu tego nie łapią z definicji:
        // karmią komponent JSON-em, który same napisały.
        var view = new SavingsReservationResponseDto(
            Guid.CreateVersion7(), "Ubezpieczenie OC", 1800m, new DateOnly(2026, 5, 1),
            1800m, ReservationStatus.Settled, new DateOnly(2026, 8, 14), Guid.CreateVersion7(), []);

        var json = System.Text.Json.JsonSerializer.Serialize(view);

        Assert.Contains("\"Settled\"", json);
        Assert.DoesNotContain("\"Status\":2", json);
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private async Task<SavingsReservationResponseDto> Reserve(string name, decimal amount, DateOnly? due) =>
        await CreateHandler().HandleAsync(new SaveReservationRequestDto(name, amount, due, 0, _budgetId), default);

    /// <summary>Wpłata na oszczędności: z konta bieżącego pieniądze WYCHODZĄ, więc kwota ujemna.</summary>
    private Guid Deposit(int year, int month, decimal amount) =>
        Add(year, month, -amount, _savingsCategoryId);

    /// <summary>Wypłata z oszczędności — wraca na bieżące, więc dodatnia.</summary>
    private Guid Withdraw(int year, int month, decimal amount) =>
        Add(year, month, amount, _savingsCategoryId);

    private Guid Add(int year, int month, decimal amount, int categoryId)
    {
        var transaction = new Transaction(
                              new DateOnly(year, month, 14),
                              amount,
                              "TEST",
                              new DateTimeOffset(year, month, 14, 0, 0, 0, TimeSpan.Zero),
                              TransactionStatus.Confirmed,
                              categoryId: categoryId,
                              budgetBusinessId: _budgetId);
        _db.Transactions.Add(transaction);
        return transaction.BusinessId;
    }
}
