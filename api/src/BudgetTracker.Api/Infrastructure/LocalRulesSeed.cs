using System.Text.Json;
using System.Text.Json.Serialization;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Reguły kategoryzacji specyficzne dla JEDNEJ instalacji — wczytywane z pliku spoza repozytorium.
///
/// <para>
/// Powód istnienia: <see cref="BaselineSeed"/> trzyma wyłącznie wzorce produktowe, bo wzorzec
/// działający tylko dzięki czyjejś historii (konkretna przychodnia, konkretny catering) jest daną
/// osobową i nie ma prawa stać w repozytorium. Ale takie reguły są <b>użyteczne</b> — bez nich
/// kategoryzacja u ich autora się pogarsza. Muszą więc mieć dom poza repo.
/// </para>
///
/// <para>
/// Ta sama konwencja co <c>training-set.csv</c> i <c>category-model.zip</c>: katalog
/// <c>data/</c> jest w <c>.gitignore</c> i README opisuje go jako <b>konfigurację środowiska</b>,
/// nie źródło. Brak pliku to stan normalny, nie awaria.
/// </para>
///
/// <para>
/// ⚠️ To NIE jest powrót do reguł w kodzie. Plik jest wejściem, nie miejscem życia reguły —
/// docelowo reguły dodaje się przez API (<c>CreateCategoryRuleCommandHandler</c>), a plik istnieje po to, żeby
/// odtworzenie bazy (<c>EnsureDeleted</c> w testach, <c>docker compose down -v</c>) nie kasowało
/// dorobku bezpowrotnie.
/// </para>
///
/// <para>
/// ⚠️ Kategoria wskazywana <b>nazwą</b>, nie identyfikatorem: plik jest edytowany ręcznie, a lista
/// Guidów nie nadaje się do czytania. Nazwy pochodzą z taksonomii <see cref="BaselineSeed"/>.
/// </para>
/// </summary>
public static class LocalRulesSeed
{
    private sealed record RuleEntry(
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("pattern")] string? Pattern,
        [property: JsonPropertyName("transactionType")] string? TransactionType,
        [property: JsonPropertyName("direction")] RuleDirection? Direction,
        [property: JsonPropertyName("priority")] int Priority,
        [property: JsonPropertyName("minAmount")] decimal? MinAmount,
        [property: JsonPropertyName("maxAmount")] decimal? MaxAmount,
        [property: JsonPropertyName("note")] string? Note);

    /// <summary>Strona przepływu z pliku; brak pola albo nieznana wartość znaczy <see cref="RuleDirection.Any"/>.</summary>
    /// <remarks>
    /// ⚠️ Pole jest w pliku opcjonalne i tak ma zostać — większość reguł nie ogranicza strony przepływu.
    /// Zanim enumy dostały numerację od 1, brak pola dawał <c>0</c>, czyli przypadkiem dokładnie <c>Any</c>.
    /// Dziś <c>0</c> nie jest żadną wartością enuma i wpadłby do bazy jako kod spoza słownika — czyli
    /// naruszenie klucza obcego przy starcie aplikacji, a nie cicha pomyłka. Dlatego brak i wartość
    /// nie do rozpoznania są tu mapowane jawnie.
    /// </remarks>
    private static RuleDirection DirectionOf(RuleEntry entry) =>
        entry.Direction is { } direction && Enum.IsDefined(direction) ? direction : RuleDirection.Any;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task SeedAsync(
        AppDbContext db, string path, ILogger logger, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return;

        List<RuleEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<RuleEntry>>(
                await File.ReadAllTextAsync(path, ct), JsonOptions);
        }
        catch (JsonException ex)
        {
            // ⚠️ Zepsuty plik NIE może wywalić startu aplikacji. To konfiguracja środowiska
            // edytowana ręcznie, a nie kod — literówka w przecinku ma kosztować brak reguł
            // lokalnych i wpis w logu, nie martwą aplikację.
            logger.LogError(ex, "Nie udało się wczytać reguł lokalnych z {Path} — pomijam.", path);
            return;
        }

        if (entries is null or { Count: 0 }) return;

        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, ct);

        // Wczytujemy TAKŻE skasowane logicznie: gdyby ktoś usunął regułę przez API, a plik nadal
        // ją zawierał, bez tego filtru seed dokładałby jej duplikat przy każdym starcie.
        var existing = await db.Set<CategoryRule>()
            .IgnoreQueryFilters()
            .Select(r => r.BusinessId)
            .ToListAsync(ct);
        var known = existing.ToHashSet();

        var added = 0;
        foreach (var entry in entries)
        {
            if (entry.Pattern is null && entry.TransactionType is null)
            {
                logger.LogWarning(
                    "Reguła lokalna bez wzorca i bez typu operacji (kategoria {Category}) — pomijam.",
                    entry.Category);
                continue;
            }

            if (!categories.TryGetValue(entry.Category, out var category))
            {
                logger.LogWarning(
                    "Reguła lokalna wskazuje nieznaną kategorię {Category} — pomijam.", entry.Category);
                continue;
            }

            // Identyfikator wyprowadzony z treści, nie losowy — dokładnie jak w BaselineSeed.
            // Dzięki temu ponowny start nie tworzy duplikatów, a poprawka wzorca w pliku daje
            // NOWĄ regułę zamiast po cichu przepisywać istniejącą (starą kasuje się przez API).
            var businessId = DeterministicGuid.For(
                $"localrule:{entry.Category}:{entry.Pattern}:{entry.TransactionType}:{entry.MinAmount}:{entry.MaxAmount}");

            if (!known.Add(businessId)) continue;

            db.Set<CategoryRule>().Add(new CategoryRule(
                    category.Id,
                    DirectionOf(entry),
                    entry.Priority,
                    Guid.Empty,
                    entry.Pattern,
                    entry.TransactionType,
                    entry.MinAmount,
                    entry.MaxAmount,
                    entry.Note)
                .WithSeedBusinessId<CategoryRule>(businessId));
            added++;
        }

        if (added == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Wczytano {Count} reguł lokalnych z {Path}.", added, path);
    }
}
