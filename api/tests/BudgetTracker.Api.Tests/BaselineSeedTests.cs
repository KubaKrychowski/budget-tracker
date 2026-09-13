using System.Text.Json;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Granica między regułami PRODUKTOWYMI a danymi osobowymi (issue #13).
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class BaselineSeedTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_baselineseed_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        await BaselineSeed.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    /// <summary>
    /// Pilnuje, żeby reguła prywatna nie wróciła do seeda — najprostszą i kuszącą poprawką
    /// kategoryzatora jest skopiowanie jej z pliku lokalnego z powrotem do kodu.
    ///
    /// <para>
    /// ⚠️ <b>Lista prywatnych wzorców NIE MOŻE stać w tym pliku.</b> Test wypisujący je jako literały
    /// wnosiłby do repozytorium dokładnie te nazwy, które ma trzymać poza nim — i to z etykietą
    /// „działają dzięki historii autora”, czyli gorzej niż sam seed. Dlatego lista pochodzi
    /// z <c>data/category-rules.json</c>, pliku poza repo.
    /// </para>
    ///
    /// <para>
    /// Skutek: na świeżym klonie (bez pliku) test nie ma czego sprawdzić i przechodzi. To jest
    /// świadome — regres jest możliwy wyłącznie tam, gdzie prywatne reguły istnieją, i właśnie tam
    /// strażnik działa. Żeby literówka w parsowaniu nie zrobiła z niego testu zawsze zielonego,
    /// przy istniejącym pliku wymagamy, żeby dało się z niego odczytać choć jeden wzorzec.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Does_not_carry_back_any_pattern_from_the_local_rules_file()
    {
        var localPatterns = LocalRulePatterns();
        if (localPatterns is null) return; // brak pliku = brak prywatnych reguł na tej maszynie

        Assert.NotEmpty(localPatterns);

        var seeded = await _db.Set<CategoryRule>()
            .Where(r => r.Pattern != null)
            .Select(r => r.Pattern!)
            .ToListAsync();

        foreach (var local in localPatterns)
        {
            Assert.DoesNotContain(seeded, s => s.Contains(local, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task No_rule_note_describes_a_correction_of_someones_real_transaction()
    {
        // Notatka w rodzaju „X to terapia, nie catering — korekta użytkownika” mówi wprost,
        // że dotyczyła czyjejś prawdziwej transakcji. Sprawdzamy sam wzorzec zdania, bez nazw.
        var notes = await _db.Set<CategoryRule>().Select(r => r.Note).ToListAsync();

        Assert.DoesNotContain(notes, n => n != null && n.Contains("korekta użytkownika", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cold_start_still_categorizes_a_generic_statement()
    {
        // ⚠️ Druga strona tej samej granicy: wyniesienie reguł NIE może zabić zimnego startu
        // (CLAUDE.md §3). Świeża baza bez pliku lokalnego ma dalej rozpoznawać zwykły wyciąg.
        var categorizer = new RuleCategorizer(_db);
        var names = await _db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name);

        async Task<string?> CategoryOf(string description, decimal amount) =>
            (await categorizer.CategorizeAsync(description, "", amount, default)).CategoryId is { } id
                ? names[id]
                : null;

        Assert.Equal("Jedzenie", await CategoryOf("jmp s a biedronka", -84m));
        Assert.Equal("Paliwo", await CategoryOf("orlen stacja nr", -250m));
        Assert.Equal("Zdrowie", await CategoryOf("apteka pod orlem", -32m));
        Assert.Equal("Catering", await CategoryOf("maczfit dieta pudelkowa", -900m));
    }

    /// <summary>
    /// Wzorce z <c>data/category-rules.json</c> albo <c>null</c>, gdy pliku nie ma.
    /// Korzeń repozytorium znajdujemy po <c>docker-compose.yml</c> — działa także w worktree,
    /// gdzie <c>.git</c> jest plikiem, a nie katalogiem.
    /// </summary>
    private static List<string>? LocalRulePatterns()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
        {
            dir = dir.Parent;
        }
        if (dir is null) return null;

        var path = Path.Combine(dir.FullName, "data", "category-rules.json");
        if (!File.Exists(path)) return null;

        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        return [.. doc.RootElement.EnumerateArray()
            .Where(e => e.TryGetProperty("pattern", out var p) && p.ValueKind == JsonValueKind.String)
            .Select(e => e.GetProperty("pattern").GetString()!)];
    }
}
