using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Dane deweloperskie — pozwalają zobaczyć dashboard z treścią, zanim powstanie import CSV.
/// Uruchamia się TYLKO w Development i TYLKO gdy nie ma jeszcze transakcji.
///
/// Kategorie i reguły NIE powstają tutaj — robi to <see cref="BaselineSeed"/>, bo są danymi
/// produkcyjnymi. Ten seed tylko z nich korzysta.
///
/// Generator ma stałe ziarno, żeby demo i testy wizualne były powtarzalne między uruchomieniami.
/// </summary>
public static class DevSeed
{
    public static async Task SeedAsync(AppDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        // Strażnik sprawdza WYŁĄCZNIE transakcje. Sprawdzanie kategorii — jak wcześniej —
        // po dodaniu BaselineSeed blokowałoby ten seed na zawsze, bo kategorie istnieją od startu.
        if (await db.Transactions.AnyAsync(ct)) return;

        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, ct);
        if (categories.Count == 0) return; // BaselineSeed jeszcze nie przeszedł — nie ma czego użyć

        var accounts = await SeedAccounts.EnsureAsync(db,
        [
            new Account("Konto osobiste", AccountType.Bank)
                .WithSeedBusinessId<Account>(DeterministicGuid.For("account:osobiste")),
            new Account("Konto wspólne", AccountType.Bank)
                .WithSeedBusinessId<Account>(DeterministicGuid.For("account:wspolne")),
            new Account("Karta lunchowa", AccountType.LunchCard)
                .WithSeedBusinessId<Account>(DeterministicGuid.For("account:lunchowa")),
        ], ct);

        // Sprzedawcy w formie, w jakiej wychodzą z wyciągów bankowych (wersaliki, doklejone kody),
        // żeby ICategorizer i normalizator miały realistyczne wejście.
        var merchants = new Dictionary<string, string[]>
        {
            ["Catering"] = ["CATERING DIETA BOX", "MACZFIT.PL", "DIETLY.PL"],
            ["Jedzenie"] = ["JMP S.A. BIEDRONKA 4821", "LIDL SP Z O O", "ZABKA Z7412"],
            ["Paliwo"] = ["ORLEN STACJA 0421", "SHELL 03 GDANSK", "AMIC POLSKA"],
            ["Rachunki"] = ["ORANGE POLSKA", "TAURON SPRZEDAZ", "UPC POLSKA"],
            ["Hobby"] = ["KINO HELIOS", "STEAM GAMES", "LEGO STORE"],
            ["Zdrowie"] = ["APTEKA GEMINI", "LUXMED SP ZOO", "DR MAX 221"],
        };

        var rng = new Random(20260831);
        var nowOffset = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(nowOffset.UtcDateTime.Date);
        var start = today.AddMonths(-2);
        var transactions = new List<Transaction>();

        // Ten sam identyfikator musi mieć konto w BudgetTracker.Identity (patrz IdentitySeeder) —
        // inaczej zalogowany deweloper nie zobaczy własnych, zaseedowanych danych. Zadeklarowany
        // wcześnie, bo transakcje niżej też dostają go jako UserId (RLS w Postgresie, nie tylko budżet).
        var ownerId = DeterministicGuid.For("dev:user:owner");

        for (var month = 0; month <= 2; month++)
        {
            var monthStart = start.AddMonths(month);
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

            for (var i = 0; i < 14; i++)
            {
                var categoryName = merchants.Keys.ElementAt(rng.Next(merchants.Count));
                var pool = merchants[categoryName];
                var day = rng.Next(1, Math.Min(28, daysInMonth) + 1);
                var date = new DateOnly(monthStart.Year, monthStart.Month, day);
                if (date > today) continue;

                var amount = Math.Round((decimal)(rng.NextDouble() * 180 + 12), 2);

                // Rozkład statusów odwzorowuje pętlę aktywnego uczenia z CLAUDE.md §3:
                // większość automatycznie, część poniżej progu trafia do przeglądu.
                var roll = rng.NextDouble();
                var (status, confidence) = roll switch
                {
                    < 0.25 => (TransactionStatus.PendingReview, (decimal?)Math.Round((decimal)(rng.NextDouble() * 0.69), 3)),
                    < 0.75 => (TransactionStatus.AutoCategorized, (decimal?)Math.Round((decimal)(rng.NextDouble() * 0.29 + 0.70), 3)),
                    < 0.90 => (TransactionStatus.ManuallyCategorized, null),
                    _ => (TransactionStatus.Confirmed, null),
                };

                transactions.Add(new Transaction(
                    date,
                    -amount,                                // ujemne = wydatek
                    pool[rng.Next(pool.Length)],
                    nowOffset,
                    status,
                    categoryId: status == TransactionStatus.PendingReview ? null : categories[categoryName].Id,
                    confidence: confidence,
                    accountId: accounts[rng.Next(accounts.Length)].Id,
                    userId: ownerId));
            }

            // Jeden duży wydatek na miesiąc — zasila kartę „największy wydatek w okresie".
            transactions.Add(new Transaction(
                new DateOnly(monthStart.Year, monthStart.Month, Math.Min(20, daysInMonth)),
                -Math.Round((decimal)(rng.NextDouble() * 500 + 800), 2),
                "SERWIS SAMOCHODU",
                nowOffset,
                TransactionStatus.Confirmed,
                categoryId: categories["Samochód"].Id,
                accountId: accounts[0].Id,
                userId: ownerId));

            // Wypłata — bez niej karta „suma przychodów" byłaby pusta.
            transactions.Add(new Transaction(
                new DateOnly(monthStart.Year, monthStart.Month, 10),
                8500m,                                      // dodatnie = przychód
                "WYNAGRODZENIE",
                nowOffset,
                TransactionStatus.Confirmed,
                accountId: accounts[0].Id,
                userId: ownerId));
        }

        db.Transactions.AddRange(transactions);

        var budgetBusinessId = DeterministicGuid.For("budget:podstawowy");
        db.Budgets.Add(new Budget(
                "Podstawowy",
                new DateOnly(today.Year, today.Month, 1),
                initialBalance: 0m,
                createdAt: nowOffset,
                userId: ownerId)
            .WithSeedBusinessId<Budget>(budgetBusinessId));

        db.BudgetItems.AddRange(categories.Values
            .Where(c => merchants.ContainsKey(c.Name))
            .Select(c => new BudgetItem(
                    budgetBusinessId, c.Id, c.Name == "Catering" ? 1500m : 800m,
                    new DateOnly(today.Year, today.Month, 1), LimitWarning.DefaultThreshold, ownerId)
                .WithSeedBusinessId<BudgetItem>(DeterministicGuid.For($"budgetitem:podstawowy:{c.Name}"))));

        await db.SaveChangesAsync(ct);
    }
}
