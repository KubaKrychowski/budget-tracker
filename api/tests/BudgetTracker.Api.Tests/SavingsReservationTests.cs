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
/// Te testy pilnują trzech reguł, których nie widać z ekranu i których pierwszy odruch przy
/// refaktorze będzie taki, żeby je „naprawić": kolejka zbierania, stabilność tej kolejki
/// i to, że rozliczenie NIE zwalnia wolnych środków.
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

    private GetSavingsReservationsQueryHandler GetHandler() => new(_db, Scope(), new SavingsCategory(_db));

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

    // ── Kolejka zbierania ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nearest_due_month_collects_first_and_surplus_flows_down()
    {
        Deposit(2026, 10, 1000m);

        await Reserve("Ubezpieczenie", 800m, new DateOnly(2026, 12, 1));
        await Reserve("Aparat", 600m, new DateOnly(2027, 3, 1));

        var response = await GetHandler().HandleAsync([_budgetId], default);
        var byName = response.Reservations.ToDictionary(r => r.Name);

        // Bliższy termin bierze całą swoją kwotę, dalszy dostaje resztę puli — NIE proporcjonalnie.
        // Podział proporcjonalny (444 / 556) dałby dwie rezerwacje, z których żadna nie jest
        // gotowa na czas, a to jest gorsze niż jedna gotowa.
        Assert.Equal(800m, byName["Ubezpieczenie"].Collected);
        Assert.Equal(200m, byName["Aparat"].Collected);
    }

    [Fact]
    public async Task Adding_a_nearer_reservation_takes_progress_away_from_the_later_one()
    {
        Deposit(2026, 10, 1000m);
        await Reserve("Aparat", 1000m, new DateOnly(2027, 3, 1));

        var before = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(1000m, before.Reservations.Single().Collected);

        await Reserve("Ubezpieczenie", 800m, new DateOnly(2026, 12, 1));

        // ⚠️ To NIE jest błąd, tylko wprost wynik kolejki — i dlatego modal dodawania ostrzega
        // o tym ZANIM użytkownik zapisze. Na ekranie wygląda jak utrata postępu.
        var after = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(200m, after.Reservations.Single(r => r.Name == "Aparat").Collected);
    }

    [Fact]
    public async Task Queue_is_stable_when_due_month_and_priority_are_equal()
    {
        Deposit(2026, 10, 500m);

        var due = new DateOnly(2027, 1, 1);
        await Reserve("Pierwsza", 400m, due);
        await Reserve("Druga", 400m, due);

        // Bez trzeciego kryterium sortowania (Id) te dwie zamieniałyby się miejscami między
        // odczytami, a „uzbierane" skakałoby przy każdym odświeżeniu. Pojedynczy odczyt tego
        // nie pokaże — stąd trzy z rzędu.
        var reads = new List<decimal>();
        for (var i = 0; i < 3; i++)
        {
            var response = await GetHandler().HandleAsync([_budgetId], default);
            reads.Add(response.Reservations.Single(r => r.Name == "Pierwsza").Collected);
        }

        Assert.Equal([400m, 400m, 400m], reads);
    }

    // ── Wolne środki ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Settling_does_not_free_up_funds()
    {
        // ⚠️ NAJWAŻNIEJSZY test w tym pliku i celowo napisany wprost. Reguła jest
        // przeciwintuicyjna (rozliczone pieniądze już zeszły z konta, więc „odejmowanie ich
        // drugi raz" wygląda na pomyłkę), więc pierwszy odruch przy refaktorze będzie taki,
        // żeby tę liczbę „naprawić". To jest decyzja użytkownika, nie przeoczenie: karta
        // nazywa się „Rezerwacje na ten rok", czyli koperta jest ROCZNA (issue #11, makieta
        // 147:96 pokazuje 9 400 − 5 000 = 4 400 przy rozliczonych 1 800).
        Deposit(2026, 9, 3000m);
        var withdrawal = Withdraw(2026, 10, 1800m);
        await _db.SaveChangesAsync();

        var reservation = await Reserve("Ubezpieczenie", 1800m, new DateOnly(2026, 5, 1));

        var before = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(1200m, before.AccountBalance);
        Assert.Equal(-600m, before.FreeFunds);

        await SettleHandler().HandleAsync(reservation.Id, new SettleReservationRequestDto(withdrawal), default);

        var after = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(-600m, after.FreeFunds);
        Assert.Equal(1800m, after.SettledTotal);

        // Rozliczenie MA widoczny skutek — po prostu innego rodzaju: zamyka rezerwację
        // i zdejmuje ją z kolejki zbierania.
        Assert.Equal(ReservationStatus.Settled, after.Reservations.Single().Status);
    }

    [Fact]
    public async Task Free_funds_subtract_every_reservation_and_may_go_negative()
    {
        Deposit(2026, 10, 1000m);
        await Reserve("Aparat", 1600m, new DateOnly(2027, 3, 1));

        var response = await GetHandler().HandleAsync([_budgetId], default);

        // Ujemne wolne środki to poprawna informacja i front pokazuje je jako osobny stan
        // („rezerwacje przekraczają stan konta o X"), a nie minus w kaflu.
        Assert.Equal(-600m, response.FreeFunds);
        Assert.Equal(1000m, response.CollectedTotal);
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

        // Brakuje 1 500 zł, tempo 500 zł/mies. → trzy miesiące od listopada, czyli luty.
        var withGoal = await GetHandler().HandleAsync([_budgetId], default);
        Assert.Equal(new DateOnly(2027, 2, 1), withGoal.CoveredBy);
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

        // Soft delete: wiersz zostaje w bazie, ale filtr globalny go nie widzi — więc wolne
        // środki wracają. To odróżnia SKASOWANIE od ROZLICZENIA, które ich nie zwalnia.
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
            1800m, ReservationStatus.Settled, new DateOnly(2026, 8, 14), Guid.CreateVersion7());

        var json = System.Text.Json.JsonSerializer.Serialize(view);

        Assert.Contains("\"Settled\"", json);
        Assert.DoesNotContain("\"Status\":2", json);
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    private async Task<SavingsReservationResponseDto> Reserve(string name, decimal amount, DateOnly due) =>
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
