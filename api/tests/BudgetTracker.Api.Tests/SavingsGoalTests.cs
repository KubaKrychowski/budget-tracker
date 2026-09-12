using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
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
/// Cel oszczędnościowy i dowód jego odporności.
///
/// Te testy NIE sprawdzają wyglądu — sprawdzają reguły, bo reguły są tu całą funkcją. Werdykt
/// miesiąca jest jedyną rzeczą, którą ten ekran mówi, a pomyłka w nim daje zdanie „ten cel jest
/// bezpieczny" postawione pod czymś, co go nie dowodzi.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class SavingsGoalTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_savings_test;Username=budget;Password=budget_dev_only";

    /// <summary>„Dziś" to 15 listopada — miesiąc w toku, żeby dało się sprawdzić bieżący miesiąc.</summary>
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
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();

        // Nazwa MUSI się zgadzać z `SavingsCategory.Name` — to jest dziś jedyny
        // sygnał odkładania (fallback do czasu #10).
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

    private GetSavingsQueryHandler GetHandler() => new(_db, Scope(), new SavingsCategory(_db));

    private SetSavingsGoalCommandHandler SetGoalHandler() => new(_db, Scope(), _clock);

    private EndSavingsGoalCommandHandler EndGoalHandler() => new(_db, Scope());

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    // ── Pomocnicze ───────────────────────────────────────────────────────────────────────

    /// <summary>Wpłata na oszczędności: z konta bieżącego pieniądze WYCHODZĄ, więc kwota ujemna.</summary>
    private void Deposit(int year, int month, decimal amount) =>
        Add(year, month, -amount, _savingsCategoryId, largeExpense: false);

    /// <summary>Wypłata z oszczędności — wraca na bieżące, więc dodatnia.</summary>
    private void Withdraw(int year, int month, decimal amount) =>
        Add(year, month, amount, _savingsCategoryId, largeExpense: false);

    /// <summary>Jednorazowy wydatek — ręczna flaga `IsLargeExpense`, czyli test obciążeniowy.</summary>
    private void OneOff(int year, int month, decimal amount) =>
        Add(year, month, -amount, _foodCategoryId, largeExpense: true);

    private void Add(int year, int month, decimal amount, int categoryId, bool largeExpense) =>
        _db.Transactions.Add(new Transaction(
                                 new DateOnly(year, month, 10),
                                 amount,
                                 "TEST",
                                 new DateTimeOffset(year, month, 10, 0, 0, 0, TimeSpan.Zero),
                                 TransactionStatus.Confirmed,
                                 categoryId: categoryId,
                                 isLargeExpense: largeExpense,
                                 budgetBusinessId: _budgetId));

    private void Goal(decimal amount, DateOnly startedOn, DateOnly? endedOn = null)
    {
        var goal = new SavingsGoal(
            _budgetId,
            amount,
            startedOn,
            new DateTimeOffset(startedOn.Year, startedOn.Month, 1, 0, 0, 0, TimeSpan.Zero));

        if (endedOn is { } ended) goal.End(ended);

        _db.SavingsGoals.Add(goal);
    }

    private async Task<SavingsMonthResponseDto> MonthAsync(int year, int month)
    {
        var response = await GetHandler().HandleAsync(null, default);
        return response.Months.Single(m => m.Month == new DateOnly(year, month, 1));
    }

    // ── Werdykt miesiąca: każdy wiersz tabeli stanów ─────────────────────────────────────

    [Fact]
    public async Task Cel_osiagniety_MIMO_jednorazowego_wydatku_to_DOWOD()
    {
        // Sedno całej funkcji: nie „odłożyłeś 1500", tylko „odłożyłeś 1500 w trudnym miesiącu".
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 10, 1500m);
        OneOff(2026, 10, 900m);
        await _db.SaveChangesAsync();

        var month = await MonthAsync(2026, 10);

        Assert.Equal(MonthVerdict.Proof, month.Verdict);
        Assert.Equal(900m, month.OneOffTotal);
    }

    [Fact]
    public async Task Ten_sam_miesiac_BEZ_jednorazowego_wydatku_to_tylko_cel_osiagniety()
    {
        // Spokojny miesiąc z osiągniętym celem jest miłą wiadomością, ale niczego nie dowodzi —
        // dowód wymaga OBU warunków naraz.
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 10, 1500m);
        await _db.SaveChangesAsync();

        Assert.Equal(MonthVerdict.GoalMet, (await MonthAsync(2026, 10)).Verdict);
    }

    [Fact]
    public async Task Niedobor_nie_jest_dowodem_niezaleznie_od_jednorazowych()
    {
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 10, 900m);
        OneOff(2026, 10, 2551m);
        await _db.SaveChangesAsync();

        Assert.Equal(MonthVerdict.GoalMissed, (await MonthAsync(2026, 10)).Verdict);
    }

    [Fact]
    public async Task Miesiac_sprzed_ustawienia_celu_nie_ma_werdyktu()
    {
        Goal(1500m, new DateOnly(2026, 6, 1));
        Deposit(2026, 3, 5000m);
        await _db.SaveChangesAsync();

        var month = await MonthAsync(2026, 3);

        Assert.Equal(MonthVerdict.NoGoal, month.Verdict);
        Assert.Null(month.Goal);
    }

    // ── Werdykt liczy się z WPŁAT, nie z netto ───────────────────────────────────────────

    [Fact]
    public async Task Wyplata_wieksza_od_wplaty_NIE_psuje_dowodu()
    {
        // ⚠️ Kryterium akceptacji wprost. Z oszczędności płaci się ubezpieczenie — po to się je
        // trzyma. Netto (1500 − 1800 = −300) dałoby „cel nieosiągnięty", choć odłożone zostało
        // dokładnie tyle, ile trzeba.
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 10, 1500m);
        Withdraw(2026, 10, 1800m);
        OneOff(2026, 10, 900m);
        await _db.SaveChangesAsync();

        var month = await MonthAsync(2026, 10);

        Assert.Equal(MonthVerdict.Proof, month.Verdict);
        Assert.Equal(1500m, month.Deposited);

        // Wypłata jest WIDOCZNA obok, nie odjęta — dzięki temu przelew tam i z powrotem
        // rzuca się w oczy, zamiast być blokowany regułą, która uderza też w uczciwy przypadek.
        Assert.Equal(1800m, month.Withdrawn);
    }

    // ── Historia celów ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Miesiac_ocenia_cel_z_TAMTEGO_czasu_a_nie_dzisiejszy()
    {
        // To jest powód, dla którego zmiana kwoty kończy poprzedni cel datą zamiast go nadpisać —
        // i dlatego próg na wykresie musi być schodkiem, a nie prostą.
        Goal(1200m, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 1));
        Goal(1500m, new DateOnly(2026, 6, 1));

        Deposit(2026, 3, 1200m);   // przy celu 1200 — wychodzi
        Deposit(2026, 7, 1200m);   // przy celu 1500 — nie wychodzi
        await _db.SaveChangesAsync();

        Assert.Equal(1200m, (await MonthAsync(2026, 3)).Goal);
        Assert.Equal(MonthVerdict.GoalMet, (await MonthAsync(2026, 3)).Verdict);

        Assert.Equal(1500m, (await MonthAsync(2026, 7)).Goal);
        Assert.Equal(MonthVerdict.GoalMissed, (await MonthAsync(2026, 7)).Verdict);
    }

    [Fact]
    public async Task Obnizenie_celu_NIE_produkuje_dowodu_wstecz()
    {
        // ⚠️ Kryterium akceptacji. Bez tej reguły wystarczyłoby 15 listopada zjechać z celem
        // poniżej tego, co się odłożyło, i bieżący miesiąc zamieniłby się w „dowód odporności".
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 11, 1000m);
        OneOff(2026, 11, 900m);
        await _db.SaveChangesAsync();

        await SetGoalHandler().HandleAsync(new SetSavingsGoalRequestDto(1000m, _budgetId), default);

        // Listopad dalej ocenia stary cel 1500 — nowy obowiązuje dopiero od grudnia.
        var november = await MonthAsync(2026, 11);
        Assert.Equal(1500m, november.Goal);
        Assert.Equal(MonthVerdict.GoalMissed, november.Verdict);
    }

    [Fact]
    public async Task PIERWSZY_cel_wolno_zadeklarowac_wstecz()
    {
        // Bez tego cała dotychczasowa historia zostaje „bez celu" i dowód — czyli jedyny powód
        // istnienia tego ekranu — nie może powstać wcześniej niż za miesiąc. Użytkownik z 20
        // miesiącami wyciągu zobaczyłby pustą tabelę i uznał funkcję za zepsutą.
        Deposit(2026, 7, 1817m);
        OneOff(2026, 7, 900m);
        await _db.SaveChangesAsync();

        await SetGoalHandler().HandleAsync(
            new SetSavingsGoalRequestDto(1500m, _budgetId, new DateOnly(2026, 1, 1)), default);

        var july = await MonthAsync(2026, 7);
        Assert.Equal(1500m, july.Goal);
        Assert.Equal(MonthVerdict.Proof, july.Verdict);
    }

    [Fact]
    public async Task ZMIANA_celu_ignoruje_date_z_zadania()
    {
        // ⚠️ Tu wsteczna data byłaby furtką: obniż cel poniżej tego, co już odłożone, z datą
        // wsteczną — i miesiąc zamienia się w „dowód odporności". Przy pierwszym celu nie ma
        // czego obniżać, przy zmianie jest.
        Goal(1500m, new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        var view = await SetGoalHandler().HandleAsync(
            new SetSavingsGoalRequestDto(800m, _budgetId, new DateOnly(2026, 1, 1)), default);

        // Obniżenie — więc od NASTĘPNEGO miesiąca, a nie od wskazanego stycznia.
        Assert.Equal(new DateOnly(2026, 12, 1), view.StartedOn);
    }

    [Fact]
    public async Task Podniesienie_celu_obowiazuje_od_RAZU()
    {
        // Odwrotny kierunek nie wymaga ochrony: podnoszenie może dowód najwyżej odebrać,
        // więc nikt nie ma interesu, żeby tak kombinować.
        Goal(1000m, new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        var view = await SetGoalHandler().HandleAsync(new SetSavingsGoalRequestDto(1500m, _budgetId), default);

        Assert.Equal(new DateOnly(2026, 11, 1), view.StartedOn);
    }

    [Fact]
    public async Task Zmiana_kwoty_KONCZY_poprzedni_cel_zamiast_go_nadpisac()
    {
        Goal(1000m, new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        await SetGoalHandler().HandleAsync(new SetSavingsGoalRequestDto(1500m, _budgetId), default);

        var goals = await _db.SavingsGoals.OrderBy(g => g.StartedOn).ToListAsync();
        Assert.Equal(2, goals.Count);

        // Stary cel ma domkniętą datę — bez tego historia dowodów odnosiłaby się do kwoty,
        // której już nie ma.
        Assert.Equal(new DateOnly(2026, 10, 1), goals[0].EndedOn);
        Assert.Equal(1000m, goals[0].Amount);
        Assert.Null(goals[1].EndedOn);
    }

    [Fact]
    public async Task Ta_sama_kwota_nie_tnie_historii_na_kawalki()
    {
        Goal(1500m, new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        await SetGoalHandler().HandleAsync(new SetSavingsGoalRequestDto(1500m, _budgetId), default);

        Assert.Single(await _db.SavingsGoals.ToListAsync());
    }

    [Fact]
    public async Task Rezygnacja_konczy_cel_data_zamiast_go_kasowac()
    {
        Goal(1500m, new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        await EndGoalHandler().HandleAsync(_budgetId, default);

        var goal = await _db.SavingsGoals.SingleAsync();
        Assert.Equal(new DateOnly(2026, 11, 1), goal.EndedOn);
        Assert.Null((await GetHandler().HandleAsync(null, default)).Goal);
    }

    [Fact]
    public async Task Kwota_zero_albo_ujemna_jest_odrzucana()
    {
        await Assert.ThrowsAsync<SavingsGoalAmountInvalidException>(
            () => SetGoalHandler().HandleAsync(new SetSavingsGoalRequestDto(0m, _budgetId), default));
    }

    [Fact]
    public async Task Rezygnacja_bez_aktywnego_celu_to_404()
    {
        await Assert.ThrowsAsync<SavingsGoalNotFoundException>(
            () => EndGoalHandler().HandleAsync(_budgetId, default));
    }

    // ── Stany ekranu ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bez_ani_jednego_oznaczonego_wydatku_ekran_wie_o_tym()
    {
        // Stan wejścia w funkcję, nie przypadek brzegowy: flaga jest ręczna, więc na zimnym
        // starcie jest ich zero. Bez tego użytkownik zobaczyłby „0 dowodów" i uznał, że funkcja
        // nie działa, zamiast dowiedzieć się, czego od niego potrzeba.
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 10, 1500m);
        await _db.SaveChangesAsync();

        var response = await GetHandler().HandleAsync(null, default);

        Assert.False(response.HasAnyLargeExpense);
        Assert.Equal(0, response.ProofCount);
    }

    [Fact]
    public async Task Brak_widocznych_wplat_jest_rozpoznawalny_od_zera_odlozonego()
    {
        // Przed #10 odkładanie widać wyłącznie przez kategorię „Oszczędności". Przelew opisany
        // inaczej nie zostanie rozpoznany — ekran ma o tym powiedzieć, a nie pokazać zero jak fakt.
        Goal(1500m, new DateOnly(2026, 1, 1));
        Add(2026, 10, -1500m, _foodCategoryId, largeExpense: false);
        await _db.SaveChangesAsync();

        var response = await GetHandler().HandleAsync(null, default);

        Assert.False(response.HasAnySavings);
        Assert.Equal(0m, response.CurrentMonth.Deposited);
    }

    [Fact]
    public async Task Biezacy_miesiac_jest_zawsze__nawet_pusty()
    {
        // Kafel „odłożone w tym miesiącu" musi mieć co pokazać od pierwszego dnia miesiąca.
        Goal(1500m, new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        var response = await GetHandler().HandleAsync(null, default);

        Assert.Equal(new DateOnly(2026, 11, 1), response.CurrentMonth.Month);
        Assert.Equal(0m, response.CurrentMonth.Deposited);
        Assert.Equal(1500m, response.CurrentMonth.Goal);
    }

    [Fact]
    public async Task Biezacy_miesiac_jest_w_HISTORII_takze_bez_transakcji()
    {
        // ⚠️ Miesiące biorą się z transakcji, więc miesiąc bez ani jednej wypadałby z listy —
        // a wtedy świeżo podniesiony cel (obowiązujący od teraz) nie miałby gdzie się pokazać
        // ani w tabeli, ani na wykresie. Użytkownik zmieniłby kwotę i nie zobaczył po niej
        // ŻADNEGO śladu. To był realny błąd, złapany na żywym API.
        Goal(1200m, new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 1));
        Goal(2000m, new DateOnly(2026, 11, 1));
        Deposit(2026, 7, 1300m);   // ostatni miesiąc Z transakcjami
        await _db.SaveChangesAsync();

        var response = await GetHandler().HandleAsync(null, default);

        var newest = response.Months[0];
        Assert.Equal(new DateOnly(2026, 11, 1), newest.Month);
        Assert.Equal(2000m, newest.Goal);

        // Dwie różne wartości progu w historii — czyli wykres ma z czego zrobić SCHODEK.
        Assert.Equal(2, response.Months.Select(m => m.Goal).Distinct().Count());
    }

    // ── Propozycja podniesienia celu ─────────────────────────────────────────────────────

    [Fact]
    public async Task Jeden_dowod_to_za_malo_na_propozycje_podniesienia()
    {
        // Dowód zgłaszamy od pierwszego — to fakt. Propozycja jest ekstrapolacją i wymaga dwóch.
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 10, 1500m);
        OneOff(2026, 10, 900m);
        await _db.SaveChangesAsync();

        var response = await GetHandler().HandleAsync(null, default);

        Assert.Equal(1, response.ProofCount);
        Assert.Null(response.RaiseSuggestion);
    }

    [Fact]
    public async Task Propozycja_bierze_NAJMNIEJSZY_zapas_a_nie_sredni()
    {
        // Średnia (900 + 2551) / 2 ≈ 1725 obiecywałaby zapas, którego w gorszym z tych miesięcy
        // nie było. To najgorszy miesiąc rozstrzyga, czy podniesiony cel się utrzyma.
        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 9, 1500m);
        OneOff(2026, 9, 2551m);
        Deposit(2026, 10, 1500m);
        OneOff(2026, 10, 900m);
        await _db.SaveChangesAsync();

        var suggestion = (await GetHandler().HandleAsync(null, default)).RaiseSuggestion;

        Assert.NotNull(suggestion);
        Assert.Equal(900m, suggestion!.Headroom);
        Assert.Equal(2400m, suggestion.SuggestedAmount);
    }

    // ── Zasięg budżetu ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Transakcje_innego_budzetu_nie_wchodza_do_rachunku()
    {
        var other = new Budget("Obcy", new DateOnly(2026, 1, 1), 0m, default);
        _db.Budgets.Add(other);
        await _db.SaveChangesAsync();

        Goal(1500m, new DateOnly(2026, 1, 1));
        Deposit(2026, 10, 500m);
        _db.Transactions.Add(new Transaction(
                                 new DateOnly(2026, 10, 10),
                                 -1000m,
                                 "CUDZE",
                                 new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero),
                                 TransactionStatus.Confirmed,
                                 categoryId: _savingsCategoryId,
                                 budgetBusinessId: other.BusinessId));
        await _db.SaveChangesAsync();

        // Budżet wskazany JAWNIE. Oba mają ten sam miesiąc, więc domyślnym zostałby nowszy
        // („Obcy") — a ten test jest o zasięgu, nie o regule wyboru domyślnego.
        var response = await GetHandler().HandleAsync([_budgetId], default);
        var october = response.Months.Single(m => m.Month == new DateOnly(2026, 10, 1));

        // 500, nie 1500 — cudzy budżet nie ma prawa domknąć naszego celu.
        Assert.Equal(500m, october.Deposited);
    }

    [Fact]
    public async Task Nieznany_budzet_to_404_a_nie_ciche_przejscie_na_domyslny()
    {
        await Assert.ThrowsAsync<BudgetNotFoundException>(
            () => GetHandler().HandleAsync([Guid.NewGuid()], default));
    }

    // ── Kontrakt z frontem ───────────────────────────────────────────────────────────────

    [Fact]
    public void Werdykt_jedzie_do_frontu_jako_NAZWA_a_nie_liczba()
    {
        // ⚠️ REGRESJA złapana na żywym API, nie w testach. Aplikacja nie rejestruje globalnego
        // `JsonStringEnumConverter`, więc bez atrybutu na typie werdykt szedł jako `0..3`,
        // a front porównuje go z nazwą — każdy miesiąc wpadłby w gałąź domyślną i tabela
        // pokazałaby „bez celu" dla wszystkiego, bez jednego błędu w konsoli.
        //
        // Testy frontu tego nie łapią z definicji: karmią komponent gotowym JSON-em, który
        // same napisały. Ten test pilnuje SZWU między stronami.
        var month = new SavingsMonthResponseDto(
            new DateOnly(2026, 10, 1), 1500m, 0m, 1500m, 900m, 1, MonthVerdict.Proof);

        var json = System.Text.Json.JsonSerializer.Serialize(month);

        Assert.Contains("\"Proof\"", json);
        Assert.DoesNotContain("\"Verdict\":3", json);
    }
}
