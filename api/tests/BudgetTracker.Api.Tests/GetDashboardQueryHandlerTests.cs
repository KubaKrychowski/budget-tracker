using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Dashboard.Contracts;
using BudgetTracker.Api.Features.Dashboard.Queries;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Testy jadą na PRAWDZIWYM Postgresie, nie na providerze InMemory — świadomie.
/// InMemory nie tłumaczy zapytań do SQL, więc przepuściłby błędy translacji LINQ
/// (np. negację nałożoną na agregat), a to jedyna klasa błędów, która realnie
/// wywala ten handler. Wymaga `docker compose up -d db`.
/// </summary>
public sealed class GetDashboardQueryHandlerTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private GetDashboardQueryHandler _handler = null!;

    /// <summary>
    /// Etykieta kubelka rozwiazana przez ten sam mechanizm co produkcja. Asercje
    /// porownuja sie do niej, a nie do wpisanego na sztywno tlumaczenia — inaczej
    /// zmiana tekstu w .resx wywracalaby testy logiki.
    /// </summary>
    private string _uncategorized = null!;

    private static readonly DateOnly From = new(2026, 3, 1);
    private static readonly DateOnly To = new(2026, 3, 31);

    /// <summary>Bilans początkowy budżetu — punkt, od którego liczy się stan konta.</summary>
    private const decimal OpeningBalance = 2000m;

    private Guid _budgetId;

    public async Task InitializeAsync()
    {
        // Interceptor tez w tescie — tak samo jak w produkcji. Odpowiada wylacznie za zamiane
        // fizycznego kasowania na logiczne; BusinessId nadaje sobie sama encja.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();

        var jedzenie = new Category("Jedzenie");
        var transport = new Category("Transport");
        _db.Categories.AddRange(jedzenie, transport);
        await _db.SaveChangesAsync();

        // Budżet MUSI powstać przed transakcjami — dashboard liczy wszystko dla wybranego
        // budżetu, więc transakcja bez `BudgetId` nie należy do żadnego i nigdzie nie wejdzie.
        var budget = new Budget("Testowy", From, OpeningBalance, default);
        _db.Budgets.Add(budget);
        _db.BudgetItems.Add(new BudgetItem(budget.BusinessId, jedzenie.Id, 1000m));
        await _db.SaveChangesAsync();
        _budgetId = budget.BusinessId;

        var now = DateTimeOffset.UtcNow;
        _db.Transactions.AddRange(
            // Jedzenie: 100 + 50 = 150
            New(new DateOnly(2026, 3, 2), -100m, "BIEDRONKA", jedzenie.Id, TransactionStatus.Confirmed, now),
            New(new DateOnly(2026, 3, 5), -50m, "LIDL", jedzenie.Id, TransactionStatus.AutoCategorized, now),
            // Transport: 400 — najdroższa kategoria i największy pojedynczy wydatek
            New(new DateOnly(2026, 3, 10), -400m, "SERWIS", transport.Id, TransactionStatus.Confirmed, now),
            // Do przeglądu — bez kategorii, ale wchodzi do sumy wydatków
            New(new DateOnly(2026, 3, 12), -25m, "NIEZNANE", null, TransactionStatus.PendingReview, now),
            New(new DateOnly(2026, 3, 13), -25m, "NIEZNANE 2", null, TransactionStatus.PendingReview, now),
            // Przychód
            New(new DateOnly(2026, 3, 15), 5000m, "WYNAGRODZENIE", null, TransactionStatus.Confirmed, now),
            // PO zakresie — nie może wpłynąć na żadną metrykę ani na bilans końcowy
            New(new DateOnly(2026, 4, 1), -9999m, "POZA OKRESEM", transport.Id, TransactionStatus.Confirmed, now),
            // PRZED zakresem — nie wchodzi do metryk okresu, ale MUSI wejść do bilansu otwarcia:
            // data nie przenosi pieniędzy, więc stan konta 1 marca zawiera już ten wydatek.
            New(new DateOnly(2026, 2, 20), -500m, "PRZED OKRESEM", transport.Id, TransactionStatus.Confirmed, now)
        );
        await _db.SaveChangesAsync();

        var localizer = CreateLocalizer();
        _uncategorized = localizer[GetDashboardQueryHandler.UncategorizedResourceKey];
        _handler = new GetDashboardQueryHandler(_db, localizer);
    }

    private Transaction New(DateOnly date, decimal amount, string desc,
        int? categoryId, TransactionStatus status, DateTimeOffset now) =>
        new(date, amount, desc, now, status, categoryId: categoryId, budgetBusinessId: _budgetId);

    /// <summary>Prawdziwy localizer na plikach .resx — nie atrapa, zeby test obejmowal tez zasoby.</summary>
    private static IStringLocalizer<SharedResource> CreateLocalizer()
    {
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()),
            NullLoggerFactory.Instance);
        return new StringLocalizer<SharedResource>(factory);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Sums_expenses_and_income_separately_by_sign()
    {
        var r = await _handler.HandleAsync(From, To, null, default);

        // 100 + 50 + 400 + 25 + 25 = 600, raportowane jako dodatnie
        Assert.Equal(600m, r.Metrics.TotalExpenses);
        Assert.Equal(5000m, r.Metrics.TotalIncome);
    }

    [Fact]
    public async Task Excludes_transactions_outside_the_requested_range()
    {
        var r = await _handler.HandleAsync(From, To, null, default);

        Assert.DoesNotContain(r.RecentTransactions, t => t.Description == "POZA OKRESEM");
        Assert.Equal(600m, r.Metrics.TotalExpenses); // gdyby wpadła, byłoby 10599
    }

    [Fact]
    public void Localizer_resolves_the_resource_instead_of_echoing_the_key()
    {
        // IStringLocalizer przy nieznalezionym zasobie zwraca KLUCZ, nie rzuca wyjatkiem.
        // Pozostale testy porownuja sie do jego wyjscia, wiec bylyby zielone nawet wtedy,
        // gdy zasoby w ogole sie nie ladują — ten test pilnuje, ze faktycznie dzialaja.
        Assert.NotEqual(GetDashboardQueryHandler.UncategorizedResourceKey, _uncategorized);
        Assert.False(string.IsNullOrWhiteSpace(_uncategorized));
    }

    [Fact]
    public async Task Ranks_categories_by_spend_descending()
    {
        var r = await _handler.HandleAsync(From, To, null, default);

        Assert.Collection(r.ByCategory,
            x => Assert.Equal(("Transport", 400m), (x.CategoryName, x.Amount)),
            x => Assert.Equal(("Jedzenie", 150m), (x.CategoryName, x.Amount)),
            // Dwie transakcje PendingReview po 25 — bez kategorii, ale to nadal wydatki.
            x => Assert.Equal((_uncategorized, 50m), (x.CategoryName, x.Amount)));
    }

    [Fact]
    public async Task Category_breakdown_reconciles_with_total_expenses()
    {
        var r = await _handler.HandleAsync(From, To, null, default);

        // To jest sedno kubełka „bez kategorii": wykres i karta „suma wydatków"
        // muszą pokazywać tę samą liczbę, inaczej user widzi sprzeczność na jednym ekranie.
        Assert.Equal(r.Metrics.TotalExpenses, r.ByCategory.Sum(c => c.Amount));
    }

    [Fact]
    public async Task Top_category_ignores_the_uncategorized_bucket()
    {
        var r = await _handler.HandleAsync(From, To, null, default);

        // Karta ma nazwać realną kategorię, nie poinformować, że coś jest nieposortowane.
        Assert.Equal("Transport", r.Metrics.TopCategoryName);
        Assert.Equal(400m, r.Metrics.TopCategoryAmount);
        Assert.NotEqual(_uncategorized, r.Metrics.TopCategoryName);
    }

    [Fact]
    public async Task Omits_uncategorized_bucket_when_everything_is_categorized()
    {
        // Okres bez transakcji PendingReview — kubełek nie ma się z czego wziąć.
        var r = await _handler.HandleAsync(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 11), null, default);

        Assert.DoesNotContain(r.ByCategory, c => c.CategoryName == _uncategorized);
        Assert.Equal(r.Metrics.TotalExpenses, r.ByCategory.Sum(c => c.Amount));
    }

    [Fact]
    public async Task Picks_the_newest_budget_that_already_started_not_the_alphabetical_first()
    {
        // „Aaa..." wygralby sortowanie po nazwie, ale dotyczy przyszlosci — nie tego okresu.
        _db.Budgets.AddRange(
            new Budget("Aaa przyszly", new DateOnly(2026, 12, 1), 0m, default),
            new Budget("Zzz starszy", new DateOnly(2026, 2, 1), 0m, default));
        await _db.SaveChangesAsync();

        var r = await _handler.HandleAsync(From, To, null, default);

        // Selektor zwraca najnowsze pierwsze.
        Assert.Equal("Aaa przyszly", r.Budgets[0].Name);

        // Ale liczby policzono z budzetu marcowego — jedynego, ktory juz sie zaczal.
        // Gdyby wybor szedl po nazwie, wpadlby budzet grudniowy: bez transakcji
        // i bez bilansu poczatkowego, wiec bilans wyszedlby 0 zamiast 5900.
        Assert.Equal(_budgetId, r.SelectedBudgetId);
        Assert.Equal(5900m, r.Metrics.BudgetBalance);
    }

    [Fact]
    public async Task Reports_largest_single_expense_as_positive_amount()
    {
        var r = await _handler.HandleAsync(From, To, null, default);

        Assert.Equal("SERWIS", r.Metrics.LargestExpenseDescription);
        Assert.Equal(400m, r.Metrics.LargestExpenseAmount);
    }

    [Fact]
    public async Task Counts_only_transactions_awaiting_review()
    {
        var r = await _handler.HandleAsync(From, To, null, default);

        Assert.Equal(2, r.Metrics.ToReviewCount);
    }

    [Fact]
    public async Task Budget_progress_has_one_point_per_transaction_plus_range_boundaries()
    {
        var r = await _handler.HandleAsync(From, To, _budgetId, default);

        // Nie „caly marzec" (31 dni) — punkt na KAZDA transakcje w oknie (6: 3/2, 3/5, 3/10,
        // 3/12, 3/13, 3/15), plus 2 punkty brzegowe: 1 marca (zadna transakcja nie jest
        // dokladnie na `from`) i 31 marca (ostatnia transakcja, 3/15, nie jest dokladnie na `to`).
        Assert.Equal(8, r.BudgetProgress.Count);

        // 1 marca nie ma jeszcze zadnej transakcji w oknie, ale stan konta NIE jest zerem:
        // to bilans poczatkowy budzetu (2000) pomniejszony o wydatek sprzed okna (-500).
        Assert.Equal(1500m, r.BudgetProgress[0].Balance);

        // Koniec marca: 1500 + (-100 -50 -400 -25 -25 +5000) = 5900.
        // Wydatek z 1 kwietnia jest POZA oknem i nie moze tu wejsc.
        Assert.Equal(5900m, r.BudgetProgress[^1].Balance);
    }

    [Fact]
    public async Task Multiple_transactions_on_the_same_day_each_get_their_own_running_balance()
    {
        // Sedno zmiany z „jeden punkt na dzien" na „jeden punkt na transakcje": dwie transakcje
        // tego samego dnia MUSZA dac dwa punkty z tym samym Date ale ROZNYM (narastajacym)
        // bilansem — nie jeden zagregowany punkt sumujacy oba naraz.
        var day = new DateOnly(2026, 3, 20);
        _db.Transactions.AddRange(
            new Transaction(
                day,
                -30m,
                "RANO",
                DateTimeOffset.UtcNow,
                TransactionStatus.Confirmed,
                budgetBusinessId: _budgetId),
            new Transaction(
                day,
                -70m,
                "WIECZOREM",
                DateTimeOffset.UtcNow,
                TransactionStatus.Confirmed,
                budgetBusinessId: _budgetId));
        await _db.SaveChangesAsync();

        var r = await _handler.HandleAsync(From, To, _budgetId, default);

        var sameDay = r.BudgetProgress.Where(p => p.Date == day).ToList();
        Assert.Equal(2, sameDay.Count);

        // Bilans przed 3/20 (z pierwszego testu tej klasy): 5900 po WSZYSTKICH transakcjach
        // do 3/15 wlacznie (pensja 3/15 juz wliczona — dzieje sie PRZED 3/20).
        // Running, nie suma dnia: pierwsza -30 -> 5870, druga -70 (na TYM juz obnizonym) -> 5800.
        Assert.Equal(5870m, sameDay[0].Balance);
        Assert.Equal(5800m, sameDay[1].Balance);
    }

    [Fact]
    public async Task Budget_with_no_transactions_in_range_still_draws_a_flat_line()
    {
        // Bez zadnej transakcji w oknie seria nie moze byc pusta — inaczej wykres nie ma czego
        // narysowac. Dwa punkty brzegowe (from/to), oba rownej bilansowi otwarcia: linia plaska,
        // bo nic sie nie wydarzylo, ale WIDOCZNA.
        var pusty = new Budget("Pusty", From, 777m, default);
        _db.Budgets.Add(pusty);
        await _db.SaveChangesAsync();

        var r = await _handler.HandleAsync(From, To, pusty.BusinessId, default);

        Assert.Equal(2, r.BudgetProgress.Count);
        Assert.Equal(From, r.BudgetProgress[0].Date);
        Assert.Equal(To, r.BudgetProgress[^1].Date);
        Assert.All(r.BudgetProgress, p => Assert.Equal(777m, p.Balance));
    }

    [Fact]
    public async Task Budget_progress_goes_down_on_expenses_and_up_on_income()
    {
        // Poprzednia wersja liczyla narastajace WYDATKI, wiec ciag byl niemalejacy.
        // Bilans musi sie ruszac w obie strony — inaczej wplyw byłby nieodrozninalny od wydatku.
        var r = await _handler.HandleAsync(From, To, _budgetId, default);

        // Punkt na transakcje, nie na dzien: 3/14 nie ma wlasnej transakcji, wiec nie ma juz
        // wlasnego punktu — porownujemy wiec punkt pensji z punktem BEZPOSREDNIO PRZED nim
        // w kolejnosci serii, nie z sasiednim dniem kalendarzowym.
        var salaryIndex = r.BudgetProgress.ToList().FindIndex(p => p.Date == new DateOnly(2026, 3, 15));
        Assert.True(salaryIndex > 0);
        var afterSalary = r.BudgetProgress[salaryIndex].Balance;
        var beforeSalary = r.BudgetProgress[salaryIndex - 1].Balance;
        Assert.Equal(5000m, afterSalary - beforeSalary);

        var beforeShopping = r.BudgetProgress.Single(p => p.Date == new DateOnly(2026, 3, 1)).Balance;
        var afterShopping = r.BudgetProgress.Single(p => p.Date == new DateOnly(2026, 3, 2)).Balance;
        Assert.Equal(-100m, afterShopping - beforeShopping);
    }

    [Fact]
    public async Task Budget_balance_metric_matches_the_last_point_of_the_chart()
    {
        // Kafel i wykres pokazuja te sama wielkosc — nie moga sie rozjechac.
        var r = await _handler.HandleAsync(From, To, _budgetId, default);

        Assert.Equal(5900m, r.Metrics.BudgetBalance);
        Assert.Equal(r.BudgetProgress[^1].Balance, r.Metrics.BudgetBalance);
    }

    [Fact]
    public async Task Every_figure_belongs_to_the_selected_budget_only()
    {
        // Regresja: `budgetId` sluzyl wylacznie do zsumowania limitow, wiec przelaczanie
        // budzetu nie zmienialo ani jednej liczby na ekranie.
        var other = new Budget("Drugi", From, 100m, default);
        _db.Budgets.Add(other);
        await _db.SaveChangesAsync();

        _db.Transactions.Add(new Transaction(
                                 new DateOnly(2026, 3, 3),
                                 -777m,
                                 "OBCY BUDZET",
                                 DateTimeOffset.UtcNow,
                                 TransactionStatus.Confirmed,
                                 budgetBusinessId: other.BusinessId));
        await _db.SaveChangesAsync();

        var mine = await _handler.HandleAsync(From, To, _budgetId, default);
        var theirs = await _handler.HandleAsync(From, To, other.BusinessId, default);

        Assert.Equal(600m, mine.Metrics.TotalExpenses);      // bez 777
        Assert.Equal(777m, theirs.Metrics.TotalExpenses);    // wylacznie 777
        Assert.NotEqual(mine.Metrics.BudgetBalance, theirs.Metrics.BudgetBalance);
        Assert.DoesNotContain(theirs.RecentTransactions, t => t.Description == "BIEDRONKA");
    }

    [Fact]
    public async Task Has_any_transactions_is_scoped_to_the_selected_budget_not_the_whole_database()
    {
        // Regresja: flaga liczyla sie kiedys po CALEJ bazie — pusty budzet obok pelnego
        // dostawalby `true` i nigdy nie pokazalby CTA importu, bo GDZIES w bazie dane sa.
        var empty = new Budget("Pusty", From, 0m, default);
        _db.Budgets.Add(empty);
        await _db.SaveChangesAsync();

        var full = await _handler.HandleAsync(From, To, _budgetId, default);
        var pusty = await _handler.HandleAsync(From, To, empty.BusinessId, default);

        Assert.True(full.HasAnyTransactions);
        Assert.False(pusty.HasAnyTransactions);
    }

    /// <summary>
    /// Punkt NA TRANSAKCJE znaczy, ze dlugosc serii zalezy od wolumenu importu, nie od dlugosci
    /// okna — a wykres bierze rok wstecz, zeby zoom dzialal bez dociagania danych. Na realnej
    /// bazie jeden budzet ma juz 1337 transakcji, wiec przy dwoch-trzech latach historii seria
    /// urosłaby do kilku tysiecy punktow w kazdej odpowiedzi.
    ///
    /// Przerzedzanie nie moze ruszyc KONCA serii: kafel „Aktualny stan budzetu" pokazuje
    /// dokladnie te sama liczbe co ostatni punkt.
    /// </summary>
    [Fact]
    public async Task Budget_progress_is_capped_but_keeps_the_first_and_last_point()
    {
        var many = new Budget("Wolumen", From, 0m, default);
        _db.Budgets.Add(many);
        await _db.SaveChangesAsync();

        // 2500 transakcji po 1 gr, rozlozonych po dniach okna — wiecej niz cap (2000).
        var rows = Enumerable.Range(0, 2500).Select(i => new Transaction(
                                                             From.AddDays(i % 31),
                                                             0.01m,
                                                             $"T{i}",
                                                             new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(i),
                                                             TransactionStatus.Confirmed,
                                                             budgetBusinessId: many.BusinessId));
        _db.Transactions.AddRange(rows);
        await _db.SaveChangesAsync();

        var r = await _handler.HandleAsync(From, To, many.BusinessId, default);

        Assert.True(r.BudgetProgress.Count <= 2000);
        Assert.Equal(From, r.BudgetProgress[0].Date);

        // Ostatni punkt to nadal PELNY bilans, a nie bilans po przerzedzonej probce.
        Assert.Equal(25m, r.BudgetProgress[^1].Balance); // 2500 * 0,01
        Assert.Equal(r.Metrics.BudgetBalance, r.BudgetProgress[^1].Balance);
    }
}
