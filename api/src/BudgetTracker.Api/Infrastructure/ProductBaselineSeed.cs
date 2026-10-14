using BudgetTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Kategorie produktów z paragonów i reguły po słowach kluczowych — dane PRODUKCYJNE dla każdego użytkownika, jak <see cref="BaselineSeed"/>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Tylko ogólne słowa kluczowe, które pomogą DOWOLNEMU polskiemu użytkownikowi („mleko”, „chleb”, „płyn do naczyń”). Nazwa konkretnej
/// marki, sklepu albo produktu z cudzego paragonu to dana osobowa i nie ma tu prawa stać — reguły użytkownika powstają z jego poprawek
/// (<c>ProductRuleLearning</c>).
/// </para>
/// <para>
/// Wzorce dopasowują się do nazwy po <c>ProductNameNormalizer</c> (małe litery, bez liczb i jednostek). Kolejność ma znaczenie:
/// „masło orzechowe” musi trafić do przetworów, nie do nabiału, więc wąskie wzorce stoją przed szerokimi. Nic tu nie wygrywa z regułą
/// nauczoną od użytkownika — ta ma priorytet <see cref="ProductRule.LearnedPriority"/>.
/// </para>
/// <para>Seed jest idempotentny: gdy kategorie wspólne już istnieją, nie robi nic. Dołożenie kategorii w przyszłości wymaga migracji danych.</para>
/// </remarks>
public static class ProductBaselineSeed
{
    private static readonly (string Name, string[] Patterns)[] Taxonomy =
    [
        ("Masło orzechowe i przetwory", ["masło orzechowe", "dżem", "powidła", "konfitur", "miód", "krem czekoladow", "ogórki kiszone", "ogórki konserwowe", "przecier", "koncentrat pomidorowy", "ketchup", "musztard", "majonez"]),
        ("Nabiał", ["mleko", "jogurt", "kefir", "maślanka", "śmietan", "masło", "ser\\b", "sery\\b", "serek", "twaróg", "mozzarell", "jaja", "jajka", "margaryn", "skyr", "budyń"]),
        ("Pieczywo", ["chleb", "bułk", "bagiet", "chałk", "drożdżówk", "rogal", "pączek", "tortill", "toast", "kajzerk", "graham"]),
        ("Mięso i wędliny", ["kurczak", "indyk", "wieprzow", "wołow", "schab", "szynk", "kiełbas", "kabanos", "parówk", "boczek", "mięso", "mielone", "pasztet", "polędwic", "ryba", "łosoś", "dorsz", "tuńczyk", "śledź", "filet"]),
        ("Warzywa i owoce", ["jabłk", "banan", "pomarańcz", "cytryn", "gruszk", "winogron", "truskawk", "malin", "borówk", "ziemniak", "marchew", "cebul", "czosnek", "pomidor", "ogórek", "papryk", "sałat", "szpinak", "brokuł", "kalafior", "kapust", "cukini", "awokado", "grzyby", "pieczark", "owoce", "warzyw"]),
        ("Napoje", ["woda", "sok\\b", "napój", "cola", "lemoniad", "herbat", "kawa", "kakao", "energetyk", "izotonik", "nektar"]),
        ("Alkohol", ["piwo", "wino", "wódka", "whisky", "whiskey", "gin\\b", "rum\\b", "likier", "cydr", "szampan", "prosecco"]),
        ("Słodycze i przekąski", ["czekolad", "baton", "cukierk", "ciastk", "wafel", "żelk", "lizak", "chips", "paluszk", "orzeszk", "popcorn", "lody", "herbatnik", "krakers", "batonik"]),
        ("Dania gotowe", ["pierogi", "pizza", "zupa", "zupka", "sałatka", "kanapk", "gołąbk", "naleśnik", "lasagne", "danie gotowe", "kotlet", "krokiet", "frytki", "hamburger"]),
        ("Produkty sypkie i makarony", ["makaron", "ryż", "kasza", "mąka", "cukier", "sól\\b", "płatki", "musli", "owsian", "fasola", "soczewic", "ciecierzyc", "olej\\b", "oliwa", "ocet", "przyprawa", "bułka tarta"]),
        ("Chemia domowa", ["płyn do naczyń", "płyn do mycia", "proszek do prania", "płyn do prania", "kapsułki do prania", "płyn do płukania", "środek czyszcz", "odświeżacz", "worki na śmieci", "gąbk", "ścierk", "tabletki do zmywarki", "odkamieniacz", "mydło w płynie", "wybielacz"]),
        ("Higiena i kosmetyki", ["szampon", "odżywka", "żel pod prysznic", "pasta do zębów", "szczoteczk", "dezodorant", "papier toaletowy", "chusteczk", "wacik", "mydło", "krem\\b", "balsam", "płyn do higieny", "podpask", "tampon", "pieluch", "golark"]),
        ("Dla zwierząt", ["karma", "żwirek", "przysmak dla psa", "przysmak dla kota", "dla psa", "dla kota"]),
        ("Opakowania i torby", ["reklamówk", "torba", "torebka", "siatk", "opakowanie"]),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.ProductCategories.AnyAsync(c => c.UserId == ProductCategory.SharedUserId, ct)) return;

        var categories = Taxonomy.ToDictionary(
            t => t.Name,
            t => new ProductCategory(t.Name, ProductCategory.SharedUserId)
                .WithSeedBusinessId<ProductCategory>(DeterministicGuid.For($"product-category:{t.Name}")));
        db.ProductCategories.AddRange(categories.Values);
        await db.SaveChangesAsync(ct);

        var rules = new List<ProductRule>();
        var priority = ProductRule.SeedPriority;
        foreach (var (name, patterns) in Taxonomy)
        {
            foreach (var pattern in patterns)
            {
                rules.Add(new ProductRule(categories[name].Id, pattern, priority, ProductRule.SharedUserId)
                    .WithSeedBusinessId<ProductRule>(DeterministicGuid.For($"product-rule:{name}:{pattern}")));
            }

            priority++;
        }

        db.ProductRules.AddRange(rules);
        await db.SaveChangesAsync(ct);
    }
}
