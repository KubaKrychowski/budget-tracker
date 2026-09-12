using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Dane pod MATERIAŁY PUBLICZNE — zrzuty ekranu na landing page (issue #12).
/// </summary>
/// <remarks>
/// ⚠️ To NIE jest <see cref="DevSeed"/> i nie zastępuje go. DevSeed celowo sypie realnymi
/// nazwami sieci handlowych, bo daje przez to realistyczne wejście dla <c>ICategorizer</c>
/// i normalizatora opisów — i ma rację, dopóki nikt tych danych nie publikuje. Ten seed jest
/// od czegoś innego: od danych, które wolno pokazać obcym ludziom w internecie.
///
/// ⚠️ <b>Wszystkie nazwy sprzedawców są zmyślone.</b> Nie ma tu ani jednej istniejącej firmy —
/// ani sieci sklepów, ani stacji paliw, ani operatora. Powód jest podwójny: zrzut z prawdziwymi
/// markami czyta się jak historia autora (a patrzący nie ma jak odróżnić danych losowych od
/// prawdziwych), a osobno — cudze znaki towarowe na stronie marketingowej to problem sam w sobie.
///
/// ⚠️ <b>Nie ma tu kategorii „Zdrowie”.</b> Świadomie. Nawet z wymyśloną nazwą apteki zrzut
/// z kategorią zdrowotną zaprasza czytelnika do wniosków o autorze, a strona nic na tej kategorii
/// nie zyskuje. Issue #12 stawia ryzyko ujawnienia danych o zdrowiu wyżej niż cokolwiek o układzie
/// strony — to jest zastosowanie tej samej zasady o jeden krok wcześniej.
///
/// Trzy zamki, wszystkie muszą puścić: środowisko Development, jawna flaga konfiguracji
/// <c>Demo:Seed</c> i PUSTA tabela transakcji. Ten seed nigdy nie ma prawa dopisać się
/// do bazy, w której są czyjeś prawdziwe wyciągi.
/// </remarks>
public static class DemoSeed
{
    /// <summary>Miesięczny cel oszczędnościowy w danych demo.</summary>
    private const decimal MonthlyGoal = 1500m;

    /// <summary>
    /// Ile miesięcy historii generujemy. Dwanaście, bo wykres na ekranie oszczędności pokazuje
    /// rok — przy krótszej historii wygląda jak błąd, a nie jak młody zbiór danych.
    /// </summary>
    private const int MonthsOfHistory = 12;

    /// <summary>
    /// Miesiąc (licząc wstecz od dzisiaj), w którym ląduje duży wydatek jednorazowy.
    /// </summary>
    /// <remarks>
    /// ⚠️ To jest sedno zrzutu do sekcji o odporności celu: w TYM miesiącu cel ma zostać
    /// dowieziony MIMO dużego wydatku. Bez takiego miesiąca ekran nie ma z czego postawić
    /// zdania, dla którego cała ta sekcja istnieje.
    /// </remarks>
    private const int ProofMonthsAgo = 4;

    /// <summary>Kwota tego wydatku — ta sama liczba, którą cytuje landing.</summary>
    private const decimal ProofOneOffAmount = 2551m;

    /// <summary>Ile zwykłych zakupów generujemy na miesiąc.</summary>
    private const int PurchasesPerMonth = 13;

    /// <summary>
    /// Zmyśleni sprzedawcy, pogrupowani kategorią z taksonomii <see cref="BaselineSeed"/>.
    /// </summary>
    /// <remarks>
    /// Nazwy są wymyślone, ale w FORMIE, w jakiej wychodzą z wyciągu (wersalik, doklejone
    /// numery placówek) — inaczej zrzut pokazywałby dane wygładzone, jakich import nie widuje.
    /// </remarks>
    private static readonly Dictionary<string, string[]> Merchants = new()
    {
        ["Jedzenie"] = ["SKLEP SPOZYWCZY KALINA 12", "MARKET POD LIPA", "DELIKATESY WRZOS 04"],
        ["Catering"] = ["PUDELKOWO SP Z O O", "DIETA SMAKOSZ"],
        ["Gastronomia"] = ["BAR MLECZNY SASANKA", "PIZZERIA KOMIN"],
        ["Paliwo"] = ["STACJA PALIW RONDO 07", "PALIWA KRESKA"],
        ["Rachunki"] = ["TELEKOM POLNOC SA", "ENERGIA WSCHOD", "SIEC DOMOWA SP Z O O"],
        ["Subskrypcje"] = ["STRUMIEN FILMOWY", "MUZYKA W CHMURZE"],
        ["Hobby"] = ["KINO ATLANTYDA", "KSIEGARNIA REGA", "PLANETA GIER"],
        ["Odzież"] = ["ODZIEZ NORDA", "BUTY MERIDIAN"],
    };

    /// <summary>Wypełnia PUSTĄ bazę rocznikiem danych demonstracyjnych.</summary>
    /// <remarks>
    /// Ten sam strażnik co w <see cref="DevSeed"/>: cokolwiek już jest w transakcjach, nie dotykamy.
    /// Brak kategorii znaczy, że <see cref="BaselineSeed"/> jeszcze nie przeszedł — wtedy nie ma
    /// z czego budować, więc wychodzimy bez śladu.
    /// </remarks>
    public static async Task SeedAsync(AppDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Transactions.AnyAsync(ct)) return;

        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, ct);
        if (categories.Count == 0) return;

        var accounts = await CreateAccountsAsync(db, ct);

        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime.Date);
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(MonthsOfHistory - 1));
        var budgetId = DeterministicGuid.For("demo:budget:domowy");

        db.Transactions.AddRange(BuildTransactions(categories, accounts, budgetId, firstMonth, today, now));

        db.Budgets.Add(new Budget("Domowy", new DateOnly(today.Year, today.Month, 1), initialBalance: 0m, createdAt: now)
            .WithSeedBusinessId<Budget>(budgetId));

        db.BudgetItems.AddRange(categories.Values
            .Where(c => Merchants.ContainsKey(c.Name))
            .Select(c => new BudgetItem(budgetId, c.Id, c.Name == "Jedzenie" ? 1400m : 700m)
                .WithSeedBusinessId<BudgetItem>(DeterministicGuid.For($"demo:budgetitem:{c.Name}"))));

        db.SavingsGoals.Add(new SavingsGoal(budgetId, MonthlyGoal, firstMonth, now)
            .WithSeedBusinessId<SavingsGoal>(DeterministicGuid.For("demo:savingsgoal")));

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Dwa konta, po których rozkładają się transakcje demo.</summary>
    private static async Task<Account[]> CreateAccountsAsync(AppDbContext db, CancellationToken ct)
    {
        Account[] accounts =
        [
            new Account("Konto osobiste", AccountType.Bank)
                .WithSeedBusinessId<Account>(DeterministicGuid.For("demo:account:osobiste")),
            new Account("Konto wspólne", AccountType.Bank)
                .WithSeedBusinessId<Account>(DeterministicGuid.For("demo:account:wspolne")),
        ];

        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync(ct);

        return accounts;
    }

    /// <summary>Rocznik transakcji: zwykłe zakupy plus stałe pozycje każdego miesiąca.</summary>
    /// <remarks>
    /// Generator ma STAŁE ZIARNO — zrzuty muszą dać się odtworzyć, inaczej po każdej regeneracji
    /// strona i materiały wizualne rozjeżdżają się bez powodu.
    ///
    /// Rozkład statusów odwzorowuje pętlę z CLAUDE.md §3: większość automatycznie, część poniżej
    /// progu do przeglądu. Zrzut ma pokazywać mechanizm, nie ideał.
    /// </remarks>
    private static List<Transaction> BuildTransactions(
        IReadOnlyDictionary<string, Category> categories,
        Account[] accounts,
        Guid budgetId,
        DateOnly firstMonth,
        DateOnly today,
        DateTimeOffset now)
    {
        var rng = new Random(20261201);
        var transactions = new List<Transaction>();

        for (var i = 0; i < MonthsOfHistory; i++)
        {
            var monthStart = firstMonth.AddMonths(i);
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var monthsAgo = MonthsOfHistory - 1 - i;

            for (var j = 0; j < PurchasesPerMonth; j++)
            {
                var categoryName = Merchants.Keys.ElementAt(rng.Next(Merchants.Count));
                var pool = Merchants[categoryName];
                var day = rng.Next(1, Math.Min(28, daysInMonth) + 1);
                var date = new DateOnly(monthStart.Year, monthStart.Month, day);
                if (date > today) continue;

                var amount = Math.Round((decimal)(rng.NextDouble() * 190 + 14), 2);

                var (status, confidence) = rng.NextDouble() switch
                {
                    < 0.18 => (TransactionStatus.PendingReview, (decimal?)Math.Round((decimal)(rng.NextDouble() * 0.69), 3)),
                    < 0.78 => (TransactionStatus.AutoCategorized, (decimal?)Math.Round((decimal)(rng.NextDouble() * 0.29 + 0.70), 3)),
                    < 0.92 => (TransactionStatus.ManuallyCategorized, null),
                    _ => (TransactionStatus.Confirmed, null),
                };

                transactions.Add(new Transaction(
                    date,
                    -amount,
                    pool[rng.Next(pool.Length)],
                    now,
                    status,
                    categoryId: status == TransactionStatus.PendingReview ? null : categories[categoryName].Id,
                    confidence: confidence,
                    budgetBusinessId: budgetId,
                    accountId: accounts[rng.Next(accounts.Length)].Id));
            }

            transactions.AddRange(
                MonthlyFixtures(monthStart, monthsAgo, today, now, categories, accounts, budgetId, rng));
        }

        return transactions;
    }

    /// <summary>Stałe pozycje miesiąca: wypłata, przelew na cel i wydatek jednorazowy.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>⚠️ Każda z nich musi wylądować NIE PÓŹNIEJ NIŻ DZIŚ. Bieżący miesiąc jest niepełny,
    /// a transakcja z datą w przyszłości to nie są „dane demo" tylko dane błędne: dashboard liczy
    /// okres kończący się dzisiaj i ją pomija, ale tabele już nie — czyli zrzuty pokazywałyby dwie
    /// różne prawdy o tym samym miesiącu.</item>
    /// <item>Wynagrodzenie — bez niego kafel przychodów jest pusty. W bieżącym miesiącu przed dniem
    /// wypłaty go NIE MA, i tak właśnie wygląda konto w połowie miesiąca.</item>
    /// <item>Wpłata na oszczędności jest UJEMNA, bo z punktu widzenia konta bieżącego przelew
    /// na oszczędnościowe naprawdę z niego wychodzi (patrz doc przy <see cref="SavingsGoal"/>).
    /// Dwa miesiące celowo poniżej celu — tabela miesięcy ma pokazywać też werdykt „nieosiągnięty",
    /// inaczej zrzut sugeruje, że aplikacja zawsze przyznaje rację.</item>
    /// <item>Wydatek w miesiącu „dowodowym" jest duży i celowo zderzony z pełną wpłatą na cel —
    /// to z tego zderzenia bierze się zdanie na landingu.</item>
    /// <item>Bieżący miesiąc też dostaje wydatek jednorazowy, i to WCZEŚNIE, o kwocie STAŁEJ
    /// (wyższej niż przelew na cel) — inaczej kafel „największy wydatek w okresie" na domyślnym,
    /// 30-dniowym widoku dashboardu pokazuje przelew na oszczędności. To prawda, ale na zrzucie
    /// wygląda jak usterka, a przy losowej kwocie zależałoby od rzutu kostką.</item>
    /// <item>⚠️ Zakup z BIEŻĄCEGO miesiąca celowo NIE jest oznaczony jako jednorazowy. Flagę stawia
    /// człowiek przy przeglądzie, więc najświeższy zakup zwykle jeszcze jej nie ma i tak wygląda
    /// prawdziwy zbiór danych. Skutek uboczny, który akurat jest pożądany: baner „dowodu" pokazuje
    /// wtedy miesiąc ZAMKNIĘTY, z pełnym obrazem, a nie miesiąc w połowie.</item>
    /// </list>
    /// </remarks>
    private static IEnumerable<Transaction> MonthlyFixtures(
        DateOnly monthStart,
        int monthsAgo,
        DateOnly today,
        DateTimeOffset now,
        IReadOnlyDictionary<string, Category> categories,
        Account[] accounts,
        Guid budgetId,
        Random rng)
    {
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

        DateOnly? DayInPast(int day)
        {
            var candidate = new DateOnly(monthStart.Year, monthStart.Month, Math.Min(day, daysInMonth));
            return candidate > today ? null : candidate;
        }

        if (DayInPast(10) is { } payday)
        {
            yield return new Transaction(
                payday,
                8500m,
                "WYNAGRODZENIE",
                now,
                TransactionStatus.Confirmed,
                categoryId: categories["Wynagrodzenie"].Id,
                budgetBusinessId: budgetId,
                accountId: accounts[0].Id);
        }

        if (DayInPast(4) is { } depositDay)
        {
            yield return new Transaction(
                depositDay,
                -(monthsAgo is 7 or 2 ? 1200m : MonthlyGoal),
                "PRZELEW WLASNY - OSZCZEDNOSCI",
                now,
                TransactionStatus.Confirmed,
                categoryId: categories["Oszczędności"].Id,
                budgetBusinessId: budgetId,
                accountId: accounts[0].Id);
        }

        if (monthsAgo == ProofMonthsAgo && DayInPast(18) is { } proofDay)
        {
            yield return new Transaction(
                proofDay,
                -ProofOneOffAmount,
                "SERWIS AUTO KOLO - NAPRAWA",
                now,
                TransactionStatus.Confirmed,
                categoryId: categories["Samochód"].Id,
                isLargeExpense: true,
                budgetBusinessId: budgetId,
                accountId: accounts[0].Id);
        }
        else if (monthsAgo is 9 or 5 or 0 && DayInPast(monthsAgo == 0 ? 2 : 22) is { } bigDay)
        {
            yield return new Transaction(
                bigDay,
                monthsAgo == 0 ? -1890m : -Math.Round((decimal)(rng.NextDouble() * 700 + 900), 2),
                monthsAgo switch
                {
                    9 => "MEBLE ROGALA - ZAMOWIENIE",
                    5 => "PRALKA - SKLEP AGD FALA",
                    _ => "ROWER MIEJSKI - SKLEP OSIE",
                },
                now,
                TransactionStatus.Confirmed,
                categoryId: categories["Wyposażenie domu"].Id,
                isLargeExpense: monthsAgo != 0,
                budgetBusinessId: budgetId,
                accountId: accounts[0].Id);
        }
    }
}
