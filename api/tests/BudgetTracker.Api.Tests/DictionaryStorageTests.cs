using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Słowniki w bazie (CLAUDE.md §5): kolumny słownikowe trzymają czytelny kod z kluczem obcym do słownika,
/// a nie liczbę, której znaczenie trzeba sprawdzać w kodzie.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class DictionaryStorageTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_dictionaries_test;Username=budget;Password=budget_dev_only";

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
    }

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private async Task<Transaction> SeedTransactionAsync()
    {
        var category = new Category("Jedzenie");
        var account = new Account("Karta", AccountType.LunchCard);
        _db.AddRange(category, account);
        await _db.SaveChangesAsync();

        _db.Add(new CategoryRule(category.Id, RuleDirection.Expense, 0, Guid.Empty, pattern: "lidl"));

        var transaction = new Transaction(
                              new DateOnly(2026, 9, 1),
                              -42m,
                              "LIDL",
                              default,
                              TransactionStatus.PendingReview,
                              accountId: account.Id);
        _db.Add(transaction);
        await _db.SaveChangesAsync();

        return transaction;
    }

    [Fact]
    public async Task Enum_columns_store_readable_codes_not_numbers()
    {
        // Sedno zmiany: przeglądając bazę, nie trzeba zaglądać do kodu, żeby wiedzieć, co znaczy wartość.
        await SeedTransactionAsync();

        Assert.Equal("PendingReview", await Scalar("select \"Status\" as \"Value\" from \"Transactions\""));
        Assert.Equal("Expense", await Scalar("select \"Direction\" as \"Value\" from \"CategoryRules\""));
        Assert.Equal("LunchCard", await Scalar("select \"Type\" as \"Value\" from \"Accounts\""));
    }

    [Fact]
    public async Task Every_enum_value_has_a_row_in_its_dictionary()
    {
        // Seed idzie z wartości enuma — nowa wartość bez wiersza w słowniku nie dałaby się zapisać (klucz obcy).
        Assert.Equal(
            Enum.GetValues<TransactionStatus>().Order(),
            (await _db.TransactionStatuses.Select(d => d.Code).ToListAsync()).Order());
        Assert.Equal(
            Enum.GetValues<RuleDirection>().Order(),
            (await _db.RuleDirections.Select(d => d.Code).ToListAsync()).Order());
        Assert.Equal(
            Enum.GetValues<AccountType>().Order(),
            (await _db.AccountTypes.Select(d => d.Code).ToListAsync()).Order());
    }

    [Fact]
    public async Task Database_rejects_a_code_outside_the_dictionary()
    {
        // Klucz obcy to jedyna ochrona przed kodem wpisanym ręcznie albo przez literówkę w migracji —
        // konwersja enuma w EF pilnuje tylko zapisów idących przez aplikację.
        await SeedTransactionAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            _db.Database.ExecuteSqlRawAsync("update \"Transactions\" set \"Status\" = 'Nieznany'"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    private Task<string> Scalar(string sql) => _db.Database.SqlQueryRaw<string>(sql).SingleAsync();
}
