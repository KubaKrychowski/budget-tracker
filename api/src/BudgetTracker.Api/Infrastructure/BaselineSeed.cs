using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Kategorie i reguły kategoryzacji — dane PRODUKCYJNE, nie deweloperskie.
///
/// Bez nich pierwszy import trafiłby w całości do przeglądu: model nie ma na czym się uczyć
/// przy pustej bazie, więc to reguły niosą jakość na starcie (CLAUDE.md §3, zimny start).
/// Dlatego seed działa w każdym środowisku, w odróżnieniu od <see cref="DevSeed"/>.
///
/// Taksonomia i wzorce nie są wymyślone — wyprowadzone z realnych wyciągów bankowych
/// (1340 transakcji, 20 miesięcy). Pokrycie na tamtym zbiorze: 97% transakcji.
///
/// ⚠️ <b>Tu trafiają wyłącznie wzorce PRODUKTOWE</b> — takie, które pomogą dowolnemu polskiemu
/// użytkownikowi przy pierwszym imporcie. Wzorzec działający tylko dlatego, że ktoś ma akurat
/// taką historię (konkretna przychodnia, konkretny catering), jest DANĄ OSOBOWĄ i nie ma prawa
/// tu stać: repozytorium bywa publiczne, a lista reguł zdrowotnych czyta się wtedy jak spis
/// świadczeniodawców autora. Takie reguły dodaje się przez API (<c>CreateCategoryRuleCommandHandler</c>)
/// albo wczytuje z <c>data/category-rules.json</c>, który leży poza repozytorium.
/// </summary>
public static class BaselineSeed
{
    /// <summary>Próg, poniżej którego zakup na stacji paliw jest sklepem, nie tankowaniem.</summary>
    private const decimal FuelMinAmount = 50m;

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        var names = new[]
        {
            "Mieszkanie", "Wyposażenie domu", "Jedzenie", "Catering", "Gastronomia",
            "Paliwo", "Samochód", "Transport publiczny", "Kredyt", "Edukacja", "Korepetycje",
            "Zdrowie", "Gotówka", "Ubezpieczenia", "Mandaty", "Rachunki", "Subskrypcje",
            "Hobby", "Odzież", "Elektronika", "Uroda", "Zakupy różne", "Przelewy na telefon",
            "Inne", "Oszczędności",

            // Strona przychodowa. Wyprowadzona z tego samego wyciągu co reszta: 81 wpływów
            // dzieli się na wypłatę, zwroty ze sklepów i przelewy od osób — i nic poza tym.
            "Wynagrodzenie", "Zwroty", "Przychody inne",
        };

        // BusinessId wyprowadzony z nazwy, nie losowy — ponowny seed musi dać te same
        // identyfikatory, inaczej wszystko, co wskazuje na zaseedowaną kategorię, wskazuje
        // po re-seedzie na nic.
        var categories = names.ToDictionary(
            n => n,
            n => new Category(n).WithSeedBusinessId<Category>(DeterministicGuid.For($"category:{n}")));
        db.Categories.AddRange(categories.Values);
        await db.SaveChangesAsync(ct);

        int Id(string name) => categories[name].Id;

        // Priorytet rośnie — sprawdzane od najniższego. Kolejność NIE jest kosmetyczna:
        // wzorce nachodzą na siebie i zła kolejność daje ciche pomyłki.
        var rules = new List<CategoryRule>();

        void Add(int priority, string category, string[] patterns, string? note = null,
            decimal? min = null, decimal? max = null, RuleDirection direction = RuleDirection.Any)
        {
            foreach (var p in patterns)
            {
                rules.Add(new CategoryRule(
                        Id(category), direction, priority,
                        pattern: p, minAmount: min, maxAmount: max, note: note)
                    .WithSeedBusinessId<CategoryRule>(DeterministicGuid.For($"rule:{category}:{p}")));
            }
        }

        // Reguła oparta na TYPIE operacji, nie na opisie — dla transakcji, których opis
        // nic nie mówi (bankomat) albo mówi tylko czyjeś nazwisko (przelew przychodzący).
        void AddByType(int priority, string category, string typePattern, string? note = null,
            RuleDirection direction = RuleDirection.Any)
        {
            rules.Add(new CategoryRule(
                    Id(category), direction, priority,
                    transactionTypePattern: typePattern, note: note)
                .WithSeedBusinessId<CategoryRule>(DeterministicGuid.For($"rule:{category}:type:{typePattern}")));
        }

        // ── Wpływy, sprawdzane najwcześniej ────────────────────────────────────────────
        // Model NIE zna kategorii przychodowych (zbiór treningowy to same wydatki), więc
        // wpływ nietrafiony regułą nie dostanie kategorii wcale — patrz HybridCategorizer.
        Add(1, "Wynagrodzenie", ["wynagrodzenie"], direction: RuleDirection.Income);
        AddByType(2, "Zwroty", "^zwrot", "Zwrot w terminalu / zwrot płatności kartą — pieniądze wracają.",
            RuleDirection.Income);
        AddByType(3, "Przychody inne", "^przelew",
            "Przelewy od osób. Świadomie jeden worek: kto i za co przelał, nie wynika z wyciągu — " +
            "ta sama decyzja co przy wychodzących „Przelewach na telefon”.",
            RuleDirection.Income);

        // ── Wydatki ────────────────────────────────────────────────────────────────────
        // Wcześnie, bo opisem wypłaty jest ADRES bankomatu. Gdyby bankomat stał w sklepie,
        // reguła opisowa (np. „biedronka”) wygrałaby i zrobiła z wypłaty zakupy spożywcze.
        AddByType(5, "Gotówka", "bankomac|bankomat",
            "Typ operacji to jedyna pewna oznaka wypłaty — opis jej nie niesie.",
            RuleDirection.Expense);


        Add(10, "Oszczędności", ["przeniesien"],
            "Przesunięcie na konto oszczędnościowe — NIE jest wydatkiem.");
        Add(20, "Mieszkanie", ["czynsz", "kaucja", "wspólnot", "wspolnot"]);
        Add(30, "Kredyt", ["kapitał", "odsetki", "rata kredytu", "spłata kredytu"]);
        Add(40, "Korepetycje", ["korepetycj"]);
        Add(50, "Edukacja", ["czesne", "uczelni", "studia", "szkoł"]);

        Add(60, "Mandaty", ["ubezpieczeniowy fundusz", "mandat", @"\bkara\b", "grzywn"],
            "UFG karze za przerwę w OC. MUSI wyprzedzać Ubezpieczenia — wzorzec 'ubezpiecz' złapałby to pierwszy.");
        Add(70, "Ubezpieczenia", ["ubezpiecz", "ergohestia", "polisa", @"\bpzu\b", @"\bwarta\b", "link4"]);

        Add(80, "Rachunki", ["tauron", "ebok", "orange", "t-mobile", @"\bupc\b", "vectra"]);
        Add(90, "Subskrypcje", ["netflix", "spotify", @"\bhbo\b", "disney", "youtube premium", "icloud", "google one"]);

        Add(100, "Catering", ["dietly", "maczfit", @"\bdieta\b", "catering"]);

        // Stacja paliw jest też sklepem: kilkanaście złotych to kawa i drożdżówka, nie tankowanie.
        // Ta reguła MUSI wyprzedzać Paliwo, inaczej każdy zakup na stacji wyjdzie jako tankowanie.
        Add(110, "Gastronomia", ["orlen", "shell", @"\bamic\b", "circle k", "stacja paliw", @"\bbp[ -]"],
            "Zakup na stacji poniżej progu = sklep, nie paliwo.", max: FuelMinAmount);
        Add(120, "Paliwo", ["orlen", "shell", @"\bamic\b", "circle k", "stacja paliw", @"\bbp[ -]"],
            min: FuelMinAmount);

        Add(130, "Gastronomia", ["restaurac", "pizz", "kebab", "mcdonald", @"\bkfc\b", "starbucks",
            @"pyszne\.pl", "glovo", "uber eats", "kawiarni", "ramen", "bistro"]);
        Add(140, "Jedzenie", [@"jmp s\.?a", "biedronka", "auchan", "lidl", "kaufland", "żabka", "zabka",
            "carrefour", @"\bdino\b", @"\bnetto\b", "stokrotka", "lewiatan", "dealz", "delikatesy",
            "społem", "spolem", "groszek", "piekarni", "mięs"],
            "Jedzenie = wyłącznie sklepy. Restauracje i dowóz idą do Gastronomii.");

        Add(150, "Samochód", ["opon", "inter cars", "serwis", "holowani", "myjnia", "parking"]);
        Add(160, "Transport publiczny", ["intercity", @"\bpkp\b", "jakdojade", "komunikacj", @"\bztm\b", @"\bkzk\b"]);

        // ⚠️ Wyłącznie wzorce ogólne i dwaj najwięksi świadczeniodawcy w kraju. Nazwa konkretnej
        // przychodni, gabinetu czy poradni NIE należy tutaj — patrz doc klasy.
        Add(170, "Zdrowie", ["terapi", "psycholog", "psychiatr", "apteka", @"\bdent", "physio",
            "luxmed", "medicover", "przychodni", "rehabilit", "okular", @"\bleki\b", "lekarz"]);
        Add(180, "Hobby", ["kino", "helios", "cinema", "steam", "playstation", "lego", @"\bbieg", "maraton"]);
        Add(190, "Odzież", ["new balance", "zalando", "reserved", "h&m", @"\bccc\b", "decathlon", "nike", "adidas", "menswear"]);
        Add(200, "Uroda", ["fryzjer", "barber", "kosmetycz"]);
        Add(210, "Zakupy różne", ["hebe", "rossmann", @"\bpepco\b", "action"],
            "Drogerie. MUSZĄ wyprzedzać Zdrowie, inaczej wzorzec „apteka” złapie je pierwszy.");

        db.CategoryRules.AddRange(rules);
        await db.SaveChangesAsync(ct);
    }
}
