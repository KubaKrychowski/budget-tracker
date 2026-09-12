using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Konwencja przekrojowa: publiczny <c>BusinessId</c> i soft delete na wszystkich encjach.
///
/// Te testy pilnują REGUŁY, nie pojedynczego feature'u — dlatego siedzą osobno.
/// Każdy z nich odpowiada kryterium akceptacji z zadania.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class BusinessIdAndSoftDeleteTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_conventions_test;Username=budget;Password=budget_dev_only;Pooling=false";

    private AppDbContext _db = null!;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        _db = NewContext();
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    /// <summary>
    /// Kontekst z interceptorem — tak samo jak w produkcji. Interceptor odpowiada wyłącznie
    /// za zamianę fizycznego kasowania na logiczne; BusinessId nadaje sobie sama encja.
    /// </summary>
    private AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options;

        return new AppDbContext(options);
    }

    // ── BusinessId ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task New_entity_gets_a_business_id_without_setting_it_explicitly()
    {
        // Kryterium akceptacji: feature nie ustawia BusinessId — encja nadaje go sobie sama
        // w inicjalizatorze, więc istnieje juz PRZED zapisem.
        var category = new Category("Testowa");
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, category.BusinessId);
    }

    [Fact]
    public async Task Business_ids_are_unique_across_rows()
    {
        _db.Categories.AddRange(
            new Category("Pierwsza"),
            new Category("Druga"));
        await _db.SaveChangesAsync();

        var ids = await _db.Categories.Select(c => c.BusinessId).ToListAsync();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Explicit_business_id_is_not_overwritten()
    {
        // Na tym stoją seedy: wpisany z góry identyfikator musi przetrwać zapis.
        var wpisany = DeterministicGuid.For("category:Jedzenie");
        _db.Categories.Add(new Category("Jedzenie").WithSeedBusinessId<Category>(wpisany));
        await _db.SaveChangesAsync();

        Assert.Equal(wpisany, (await _db.Categories.SingleAsync()).BusinessId);
    }

    [Fact]
    public void Seed_run_twice_produces_the_same_business_ids()
    {
        // Kryterium akceptacji. Gdyby identyfikatory były losowe, po re-seedzie wszystko,
        // co wskazuje na zaseedowaną kategorię, wskazywałoby na nic.
        Assert.Equal(
            DeterministicGuid.For("category:Jedzenie"),
            DeterministicGuid.For("category:Jedzenie"));

        Assert.NotEqual(
            DeterministicGuid.For("category:Jedzenie"),
            DeterministicGuid.For("category:Paliwo"));
    }

    // ── Soft delete ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_stamps_deleted_at_instead_of_deleting_the_row()
    {
        // Kryterium akceptacji: przypadkowe db.Remove(x) nie może skasować wiersza fizycznie.
        var category = new Category("Do skasowania");
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        var wRzeczywistosci = await _db.Categories
            .IgnoreQueryFilters()
            .SingleAsync(c => c.Name == "Do skasowania");

        Assert.Equal(_clock.GetUtcNow(), wRzeczywistosci.DeletedAt);
    }

    [Fact]
    public async Task Deleted_row_is_invisible_without_ignore_query_filters()
    {
        var category = new Category("Niewidoczna");
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        // Świeży kontekst — inaczej patrzylibyśmy na cache change trackera, nie na filtr.
        await using var fresh = NewContext();

        Assert.Empty(await fresh.Categories.Where(c => c.Name == "Niewidoczna").ToListAsync());
        Assert.Single(await fresh.Categories.IgnoreQueryFilters()
            .Where(c => c.Name == "Niewidoczna").ToListAsync());
    }

    [Fact]
    public async Task A_category_can_reuse_the_name_of_a_deleted_one()
    {
        // Kryterium akceptacji: indeks unikalny musi być CZĘŚCIOWY. Bez filtra skasowany
        // wiersz dalej zajmowałby nazwę i soft delete byłby nieodróżnialny od blokady.
        var pierwsza = new Category("Jedzenie");
        _db.Categories.Add(pierwsza);
        await _db.SaveChangesAsync();

        _db.Categories.Remove(pierwsza);
        await _db.SaveChangesAsync();

        await using var fresh = NewContext();
        fresh.Categories.Add(new Category("Jedzenie"));

        // Brak wyjątku = indeks jest częściowy.
        await fresh.SaveChangesAsync();
        Assert.Single(await fresh.Categories.Where(c => c.Name == "Jedzenie").ToListAsync());
    }

    [Fact]
    public async Task Deleted_transactions_stop_counting_as_duplicates()
    {
        // ⚠️ Pułapka opisana w planie: reset budżetu + ponowny import tego samego pliku
        // MUSI wpuścić transakcje z powrotem. Gdyby deduplikacja widziała skasowane wiersze,
        // objawem byłoby „zaimportowano 0 wierszy" BEZ żadnego błędu.
        var budget = new Budget("Testowy", new DateOnly(2026, 9, 1), 0m, default);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        var transaction = new Transaction(
                              new DateOnly(2026, 9, 2),
                              -100m,
                              "SKLEP",
                              _clock.GetUtcNow(),
                              TransactionStatus.Imported,
                              externalReference: "R1",
                              budgetBusinessId: budget.BusinessId);
        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

        _db.Transactions.Remove(transaction);
        await _db.SaveChangesAsync();

        await using var fresh = NewContext();

        // To jest dokładnie zapytanie, którym ExistingTransactionKeys szuka duplikatów.
        var kandydaci = await fresh.Transactions
            .Where(t => t.BudgetBusinessId == budget.BusinessId)
            .ToListAsync();

        Assert.Empty(kandydaci);
    }

    // ── Kaskady ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void No_relation_is_configured_as_cascade()
    {
        // Kryterium akceptacji. Kaskada w bazie jest naładowaną bronią: nic jej nie wywoła,
        // dopóki ktoś nie doda endpointu kasującego — i wtedy zabierze dane po cichu.
        var kaskady = _db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.ClientCascade)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name} → {fk.PrincipalEntityType.ClrType.Name}")
            .ToList();

        Assert.Empty(kaskady);
    }

    [Fact]
    public void Every_entity_has_business_id_and_soft_delete()
    {
        // Nowa encja bez tych dwóch pól wypada z konwencji — ten test to wyłapie,
        // zanim wypadnie z niej po cichu połowa API.
        //
        // Jedyny dopuszczony wyjątek to słowniki (DictionaryEntity): kod jako klucz, bez BusinessId
        // i soft delete. Ale każdy musi być seedowany — inaczej dałoby się podpiąć pod tę bazę zwykłą
        // tabelę danych użytkownika tylko po to, żeby zgubić soft delete.
        var bezKonwencji = _db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => !typeof(Entity).IsAssignableFrom(t) && !typeof(DictionaryEntity).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        var designTimeModel = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(_db).Model;

        var slownikiBezSeeda = designTimeModel.GetEntityTypes()
            .Where(e => typeof(DictionaryEntity).IsAssignableFrom(e.ClrType) && !e.GetSeedData().Any())
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.Empty(bezKonwencji);
        Assert.Empty(slownikiBezSeeda);
    }
}
