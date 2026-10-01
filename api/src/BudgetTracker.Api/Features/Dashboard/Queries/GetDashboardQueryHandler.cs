using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Dashboard.Contracts;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Api.Features.Dashboard.Queries;

/// <summary>
/// Cały ekran dashboardu w jednym round-tripie — wszystkie liczby dla JEDNEGO, wybranego budżetu.
/// Dostęp do danych zostaje tutaj, nie w warstwie repozytoriów, żeby przyszły multi-user dało się
/// dołożyć global query filtrem.
/// </summary>
public sealed class GetDashboardQueryHandler(AppDbContext db, IStringLocalizer<SharedResource> localizer)
{
    private const int RecentTransactionLimit = 5;

    /// <summary>
    /// Klucz zasobu dla kubełka wydatków bez kategorii (status <see cref="TransactionStatus.PendingReview"/>).
    /// Bez tego kubełka wykres nie sumowałby się do karty „suma wydatków" i obie liczby by się rozjeżdżały.
    /// Etykieta idzie przez IStringLocalizer, więc testy muszą rozwiązać ją tak samo jak produkcja.
    /// </summary>
    public const string UncategorizedResourceKey = "Category_Uncategorized";

    /// <summary>
    /// Górna granica liczby punktów serii. Przy jednym punkcie NA TRANSAKCJĘ rozmiar serii
    /// zależy od wolumenu importu, nie od długości okna — a wykres bierze rok wstecz, żeby
    /// zoom i pan działały bez dociągania danych. Na tej bazie jeden budżet ma już 1337
    /// transakcji, więc to nie jest hipoteza.
    /// </summary>
    private const int MaxProgressPoints = 2000;

    /// <summary>Liczy dashboard dla domkniętego obustronnie okresu <paramref name="from"/>–<paramref name="to"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Wybór budżetu MUSI poprzedzać pierwsze zapytanie o transakcje — wszystkie liczby na tym ekranie
    /// dotyczą jednego budżetu. Wcześniej <paramref name="budgetId"/> służył wyłącznie do zsumowania limitów,
    /// więc przełączanie budżetu nie zmieniało niczego poza limitem.</item>
    /// <item>Znak decyduje o kierunku: ujemne = wydatek, dodatnie = przychód (<see cref="Transaction.Amount"/>).
    /// Wydatki raportujemy jako wartość dodatnią, bo tak są prezentowane w UI.</item>
    /// <item>Bilans liczy się od bilansu OTWARCIA: bilans początkowy budżetu plus wszystko, co wydarzyło się
    /// PRZED oknem. Bez tego przełączenie zakresu dat zmieniałoby stan konta, a data nie przenosi pieniędzy.</item>
    /// <item><see cref="DashboardResponseDto.HasAnyTransactions"/> liczone CELOWO dla wybranego budżetu, nie globalnie —
    /// budżet bez transakcji ma pokazać CTA importu, nawet gdy inne budżety w bazie mają dane.</item>
    /// </list>
    /// </remarks>
    public async Task<DashboardResponseDto> HandleAsync(
        DateOnly from, DateOnly to, Guid? budgetId, CancellationToken ct)
    {
        var budgetRows = await LoadBudgetsAsync(ct);
        var budgets = budgetRows
            .Select(b => new BudgetOptionResponseDto(b.BusinessId, b.Name, b.Month, b.DisabledAt is not null))
            .ToList();

        var selected = SelectBudget(budgetRows, budgetId, to);
        var selectedBudgetBusinessId = selected?.BusinessId;

        var ofBudget = db.Transactions.Where(t => t.BudgetBusinessId == selectedBudgetBusinessId);
        var inRange = ofBudget.Where(t => t.Date >= from && t.Date <= to);

        // Przelewy do/z powiązanego budżetu oszczędnościowego (SavingsTransferMatcher) pomijają się
        // z sum wydatków/przychodów — nie są realnym ruchem majątku. Bilans (BuildBudgetProgressAsync)
        // liczy się z `ofBudget`, nie z tej zmiennej, i ich celowo NIE pomija.
        var forTotals = inRange.Where(t => t.SavingsTransferBudgetBusinessId == null);

        var sums = await SumRangeAsync(inRange, ct);
        var totalExpenses = -sums.Expenses;
        var totalIncome = sums.Income;
        var toReviewCount = sums.ToReview;

        var namedCategories = await SpendByNamedCategoryAsync(forTotals, ct);
        var byCategory = WithUncategorized(namedCategories, -sums.Uncategorized);

        var largest = await forTotals.Where(t => t.Amount < 0)
            .OrderBy(t => t.Amount)
            .Select(t => new { t.Description, t.Amount })
            .FirstOrDefaultAsync(ct);

        // ⚠️ Bilans z salda banku, nie „początkowy + suma" — patrz BalanceLedger. Ta sama księga zasila listę budżetów,
        // podsumowanie importu i konto oszczędnościowe, więc wszystkie ekrany pokazują to samo.
        var ledger = await BudgetLedgers.LoadAsync(db, selectedBudgetBusinessId, selected?.InitialBalance ?? 0m, ct);
        var openingBalance = ledger.BalanceBefore(from);
        var budgetProgress = BuildBudgetProgress(from, to, ledger);

        var recent = await inRange
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Take(RecentTransactionLimit)
            .Select(t => new RecentTransactionResponseDto(
                t.BusinessId, t.Date, t.Description, t.Amount,
                db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefault(),
                t.Status.ToString()))
            .ToListAsync(ct);

        return new DashboardResponseDto(
            new DashboardMetricsResponseDto(
                totalExpenses, totalIncome,
                BudgetBalance: budgetProgress.Count == 0
                    ? openingBalance
                    : budgetProgress[^1].Balance,
                TopCategoryName: namedCategories.FirstOrDefault()?.CategoryName,
                TopCategoryAmount: namedCategories.FirstOrDefault()?.Amount ?? 0m,
                LargestExpenseDescription: largest?.Description,
                LargestExpenseAmount: largest is null ? 0m : -largest.Amount,
                ToReviewCount: toReviewCount,
                ReservedOnRegular: await ReservedOnRegularAsync(selectedBudgetBusinessId, ct)),
            byCategory,
            budgetProgress,
            recent,
            budgets,
            SelectedBudgetId: selected?.BusinessId,
            HasAnyTransactions: ledger.Entries.Count > 0);
    }

    /// <summary>Ile na zwykłym koncie budżetu jest zarezerwowane na cele — wpłaty ze zwykłego konta na rezerwacje nierozliczone.</summary>
    /// <remarks>Suma w pamięci: wpłaty siedzą w kolumnie jsonb (patrz <c>SavingsAccount.ContributedAsync</c>).</remarks>
    private async Task<decimal> ReservedOnRegularAsync(Guid? budget, CancellationToken ct)
    {
        if (budget is null) return 0m;

        return (await db.SavingsReservations
                .Where(r => r.BudgetBusinessId == budget && r.SettledAt == null)
                .ToListAsync(ct))
            .Sum(r => r.ContributedFrom(ContributionSource.Regular));
    }

    /// <summary>Budżety od najnowszego miesiąca — lista przełącznika i kandydaci na budżet domyślny.</summary>
    /// <remarks>
    /// Sortowanie po wewnętrznym <c>Id</c> daje stabilną kolejność budżetów z tego samego miesiąca; samo <c>Id</c>
    /// nie wychodzi na front. Transakcje filtruje publiczny <c>BusinessId</c>, bo
    /// <see cref="Transaction.BudgetBusinessId"/> to zwykła kolumna bez relacji EF.
    /// </remarks>
    private async Task<List<BudgetRow>> LoadBudgetsAsync(CancellationToken ct) =>
        await db.Budgets
            .OrderByDescending(b => b.Month)
            .ThenByDescending(b => b.Id)
            .Select(b => new BudgetRow(b.BusinessId, b.Name, b.Month, b.InitialBalance, b.DisabledAt))
            .ToListAsync(ct);

    /// <summary>Budżet, dla którego liczy się ekran: wskazany albo domyślny.</summary>
    /// <remarks>
    /// Bez wskazania bierzemy budżet domyślny: najnowszy, który już się zaczął przed końcem oglądanego okresu;
    /// gdy żaden nie jest wystarczająco stary — najstarszy istniejący.
    ///
    /// Wskazany, ale nieznany <c>BusinessId</c> NIE może po cichu przejść na domyślny: literówka w adresie
    /// pokazywałaby liczby innego budżetu jako swoje. Stąd 404 zamiast fallbacku. Reguła wyboru jest WSPÓLNA
    /// dla całej aplikacji (<see cref="BudgetScope"/>) — dashboard, lista transakcji i oszczędności muszą
    /// domyślnie pokazywać ten sam budżet, inaczej każdy ekran ma rację osobno, a razem kłamią.
    /// </remarks>
    private static BudgetRow? SelectBudget(List<BudgetRow> budgets, Guid? requested, DateOnly to)
    {
        var candidates = budgets.Select(b => new BudgetCandidate(b.BusinessId, b.Month)).ToList();
        var selectedId = BudgetScope.Resolve(candidates, requested is { } one ? [one] : null, to)
            .FirstOrDefault();

        return budgets.FirstOrDefault(b => b.BusinessId == selectedId);
    }

    /// <summary>Wydatki per NAZWANA kategoria, jako wartości dodatnie, od największej.</summary>
    /// <remarks>
    /// Negacja MUSI być poza zapytaniem. EF nie tłumaczy <c>-g.Sum(...)</c> wewnątrz projekcji do konstruktora —
    /// sumujemy w SQL, znak odwracamy po materializacji.
    /// </remarks>
    private async Task<List<CategorySpendResponseDto>> SpendByNamedCategoryAsync(
        IQueryable<Transaction> inRange, CancellationToken ct)
    {
        var totals = await inRange
            .Where(t => t.Amount < 0 && t.CategoryId != null)
            .Join(db.Categories, t => t.CategoryId, c => (int?)c.Id, (t, c) => new { t.Amount, c.BusinessId, c.Name })
            .GroupBy(x => new { x.BusinessId, x.Name })
            .Select(g => new { g.Key.BusinessId, g.Key.Name, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        return totals
            .Select(x => new CategorySpendResponseDto(x.BusinessId, x.Name, -x.Total))
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    /// <summary>Sumy okresu jednym zapytaniem: wydatki, przychody, wydatki bez kategorii i licznik „do weryfikacji".</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>⚠️ To jedno zapytanie zastępuje cztery. Baza stoi w innym regionie niż aplikacja, więc każde zapytanie to
    /// ok. 125 ms samego łącza — liczba zapytań na ekran jest tu ważniejsza niż koszt pojedynczego.</item>
    /// <item>Wydatki, przychody i kubełek „bez kategorii" pomijają przelewy do/z budżetu oszczędnościowego
    /// (<see cref="Transaction.SavingsTransferBudgetBusinessId"/>), a licznik „do weryfikacji" liczy się z CAŁEGO okresu —
    /// tak jak przed scaleniem. Warunki siedzą więc w sumach warunkowych, nie w wspólnym filtrze.</item>
    /// <item>Pusty okres daje zero wierszy z <c>GROUP BY</c>, stąd <c>default</c> (same zera).</item>
    /// </list>
    /// </remarks>
    private static async Task<RangeSums> SumRangeAsync(IQueryable<Transaction> inRange, CancellationToken ct) =>
        await inRange
            .GroupBy(_ => 1)
            .Select(g => new RangeSums(
                g.Sum(t => t.SavingsTransferBudgetBusinessId == null && t.Amount < 0 ? t.Amount : 0m),
                g.Sum(t => t.SavingsTransferBudgetBusinessId == null && t.Amount > 0 ? t.Amount : 0m),
                g.Sum(t => t.SavingsTransferBudgetBusinessId == null && t.Amount < 0 && t.CategoryId == null ? t.Amount : 0m),
                g.Count(t => t.Status == TransactionStatus.PendingReview)))
            .FirstOrDefaultAsync(ct);

    /// <summary>Seria wykresu kategorii: nazwane kategorie plus kubełek „bez kategorii", gdy coś w nim jest.</summary>
    /// <remarks>
    /// Wykres dostaje kubełek „bez kategorii", żeby sumował się do sumy wydatków. „Najdroższa kategoria" go NIE
    /// dostaje — ta karta ma nazwać realną kategorię, a nie poinformować, że coś jest jeszcze nieposortowane.
    /// <c>CategoryId = null</c> jest tu SENTINELEM „bez kategorii", nie brakiem danych — front filtruje listę
    /// transakcji po nim (<c>uncategorized=true</c>), nigdy po przetłumaczonej nazwie.
    /// </remarks>
    private List<CategorySpendResponseDto> WithUncategorized(
        List<CategorySpendResponseDto> namedCategories, decimal uncategorized)
    {
        return uncategorized > 0m
            ? [.. namedCategories, new CategorySpendResponseDto(null, localizer[UncategorizedResourceKey], uncategorized)]
            : namedCategories;
    }

    /// <summary>
    /// Bilans budżetu PO KAŻDEJ TRANSAKCJI — seria pod wykres „Stan budżetu".
    /// </summary>
    /// <remarks>
    /// ⚠️ Ta metoda liczyła wcześniej NARASTAJĄCE WYDATKI od zera, zestawione z płaską linią
    /// limitu. To nie jest stan budżetu: pokazywało, ile wydano w oknie, a nie ile jest na
    /// koncie. Kolejna wersja liczyła jeden zagregowany punkt NA KAŻDY KALENDARZOWY DZIEŃ
    /// okna (nawet bez transakcji) — czytelne na siatce, ale sztuczne: dzień bez ruchu i dzień
    /// z dziesięcioma transakcjami wyglądały tak samo „gęsto".
    ///
    /// Teraz punkt powstaje na KAŻDĄ transakcję (running balance, nie suma dnia) — kilka
    /// transakcji jednego dnia daje kilka punktów tego samego dnia, a dni bez ruchu nie dają
    /// punktu wcale (na wykresie: prosta, ukośna linia między sąsiednimi transakcjami zamiast
    /// sztucznie gęstej siatki). Dwa punkty brzegowe dokładają się TYLKO gdy nie pokrywają się
    /// z prawdziwą transakcją: jeden na <paramref name="from"/> z <paramref name="openingBalance"/>
    /// (żeby linia nie zaczynała się od razu OD stanu po pierwszej transakcji, chowając bilans,
    /// z którym budżet wszedł w okno) i jeden na <paramref name="to"/> z bilansem końcowym (żeby
    /// wykres nie urywał się w środku, gdy ostatnia transakcja jest przed końcem okna — kafel
    /// „Aktualny stan budżetu" i ostatni punkt tej serii muszą dalej pokazywać tę samą liczbę).
    ///
    /// Kolejność w dniu ustala księga (<see cref="BalanceLedger"/>): wg ciągłości sald z banku, a gdy się nie da — wg <c>Id</c>.
    /// <c>Id</c> nie wychodzi z API.
    /// </remarks>
    private static List<BudgetPointResponseDto> BuildBudgetProgress(DateOnly from, DateOnly to, BalanceLedger ledger)
    {
        var transactions = ledger.Entries.Where(e => e.Date >= from && e.Date <= to).ToList();

        var points = new List<BudgetPointResponseDto>();
        var balance = ledger.BalanceBefore(from);

        if (transactions.Count == 0 || transactions[0].Date != from)
        {
            points.Add(new BudgetPointResponseDto(from, balance));
        }

        // Bilans po KAŻDEJ transakcji bierzemy z księgi (saldo z banku), nie kumulujemy kwot: kumulacja odtwarzałaby dokładnie
        // ten rozjazd z bankiem, który księga ma usunąć.
        foreach (var t in transactions)
        {
            balance = t.Balance;
            points.Add(new BudgetPointResponseDto(t.Date, balance));
        }

        if (points.Count == 0 || points[^1].Date != to)
        {
            points.Add(new BudgetPointResponseDto(to, balance));
        }

        return Downsample(points);
    }

    /// <summary>
    /// Przerzedza serię, gdy punktów jest więcej niż <see cref="MaxProgressPoints"/> — równym
    /// krokiem, z ZACHOWANIEM pierwszego i ostatniego punktu.
    /// </summary>
    /// <remarks>
    /// Ostatni jest nienaruszalny, bo kafel „Aktualny stan budżetu" pokazuje dokładnie tę samą
    /// liczbę: gdyby przerzedzanie go zjadło, wykres i kafel rozjechałyby się na oczach
    /// użytkownika. Pierwszy trzyma bilans otwarcia, z którym budżet wszedł w okno.
    ///
    /// Przerzedzamy dopiero POWYŻEJ progu, więc przy dzisiejszych danych (rząd setek punktów)
    /// nie robi to nic — to bezpiecznik na wolumen, nie stałe pogorszenie dokładności.
    /// </remarks>
    private static List<BudgetPointResponseDto> Downsample(List<BudgetPointResponseDto> points)
    {
        if (points.Count <= MaxProgressPoints) return points;

        var step = (double)(points.Count - 1) / (MaxProgressPoints - 1);
        var thinned = new List<BudgetPointResponseDto>(MaxProgressPoints);

        for (var i = 0; i < MaxProgressPoints - 1; i++)
        {
            thinned.Add(points[(int)(i * step)]);
        }

        thinned.Add(points[^1]);
        return thinned;
    }

    /// <summary>Sumy okresu z <see cref="SumRangeAsync"/>; wydatki i „bez kategorii" jako wartości ujemne, jak w bazie.</summary>
    private readonly record struct RangeSums(decimal Expenses, decimal Income, decimal Uncategorized, int ToReview);

    /// <summary>Budżet w kształcie potrzebnym ekranowi — bez kolumn, których dashboard nie czyta.</summary>
    private sealed record BudgetRow(
        Guid BusinessId, string Name, DateOnly Month, decimal InitialBalance, DateTimeOffset? DisabledAt);
}
