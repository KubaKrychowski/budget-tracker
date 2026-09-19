using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Hasła kont dev/demo pochodzą z konfiguracji lokalnej (user-secrets). Brak albo odrzucone hasło NIE może wywracać startu
/// serwera tożsamości — wtedy przestaje działać logowanie na wszystkie konta i ekrany administratora, a nie tylko na to jedno.
/// Testy używają prawdziwego <see cref="UserManager{TUser}"/> z domyślną polityką haseł i magazynem w pamięci.
/// </summary>
public sealed class FixedAccountSeederTests
{
    private const string Key = "Seed:DevPassword";
    private const string Email = "dev@budgettracker.local";
    private const string ValidPassword = "Abcdef1!";

    private static readonly Guid Id = Guid.CreateVersion7();

    private readonly MemoryUserStore store = new();
    private readonly UserManager<ApplicationUser> users;

    public FixedAccountSeederTests() => users = CreateUserManager(store);

    private static UserManager<ApplicationUser> CreateUserManager(MemoryUserStore store) => CreateUserManager(store, new LocalizedIdentityErrorDescriber(TestLocalizer.Create()));

    /// <summary>Walidator hasła musi dostać ten sam describer co UserManager — w aplikacji oba biorą go z DI.</summary>
    private static UserManager<ApplicationUser> CreateUserManager(MemoryUserStore store, IdentityErrorDescriber describer) => new(
        store,
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        new PasswordHasher<ApplicationUser>(),
        [new UserValidator<ApplicationUser>(describer)],
        [new PasswordValidator<ApplicationUser>(describer)],
        new UpperInvariantLookupNormalizer(),
        describer,
        null!,
        NullLogger<UserManager<ApplicationUser>>.Instance);

    private static (FixedAccountSeeder Seeder, ListLogger Log) CreateSeeder(string? password)
    {
        var settings = new Dictionary<string, string?>();
        if (password is not null) settings[Key] = password;
        var log = new ListLogger();
        return (new FixedAccountSeeder(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), log), log);
    }

    [Fact]
    public async Task A_missing_password_only_warns_and_does_not_create_the_account()
    {
        var (seeder, log) = CreateSeeder(password: null);

        var exists = await seeder.SeedAsync(users, Id, Email, Key);

        Assert.False(exists);
        Assert.Empty(store.Accounts);
        var warning = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(Key, warning.Message);
        Assert.Contains(Email, warning.Message);
    }

    [Fact]
    public async Task A_blank_password_is_treated_as_missing()
    {
        var (seeder, log) = CreateSeeder(password: "");

        Assert.False(await seeder.SeedAsync(users, Id, Email, Key));

        Assert.Empty(store.Accounts);
        Assert.Equal(LogLevel.Warning, Assert.Single(log.Entries).Level);
    }

    [Fact]
    public async Task A_password_rejected_by_the_policy_only_warns_and_never_logs_the_password()
    {
        // ⚠️ To był prawdziwy błąd: hasło bez znaku specjalnego kończyło się wyjątkiem w StartAsync i serwer nie wstawał.
        const string weak = "Passw0rdBezZnakuSpecjalnego";
        var (seeder, log) = CreateSeeder(weak);

        var exists = await seeder.SeedAsync(users, Id, Email, Key);

        Assert.False(exists);
        Assert.Empty(store.Accounts);
        var warning = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(Key, warning.Message);
        // Ten sam opis błędu, który zobaczył deweloper w konsoli — z tego samego describera co w aplikacji.
        Assert.Contains(new LocalizedIdentityErrorDescriber(TestLocalizer.Create()).PasswordRequiresNonAlphanumeric().Description, warning.Message);
        Assert.DoesNotContain(weak, warning.Message);
    }

    [Fact]
    public async Task A_skipped_account_is_created_by_a_later_run_once_the_password_is_fixed()
    {
        // Gdyby konto powstawało BEZ hasła, kolejny start widziałby je jako istniejące i nigdy nie ustawił hasła.
        await CreateSeeder(password: null).Seeder.SeedAsync(users, Id, Email, Key);

        var (seeder, log) = CreateSeeder(ValidPassword);
        var exists = await seeder.SeedAsync(users, Id, Email, Key);

        Assert.True(exists);
        var account = Assert.Single(store.Accounts.Values);
        Assert.Equal(Id, account.Id);
        Assert.True(account.EmailConfirmed);
        Assert.NotNull(account.PasswordHash);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task An_existing_account_needs_no_password_and_gives_no_warning()
    {
        await CreateSeeder(ValidPassword).Seeder.SeedAsync(users, Id, Email, Key);
        var hashBefore = store.Accounts[Id].PasswordHash;

        var (seeder, log) = CreateSeeder(password: null);
        var exists = await seeder.SeedAsync(users, Id, Email, Key);

        Assert.True(exists);
        Assert.Empty(log.Entries);
        Assert.Equal(hashBefore, store.Accounts[Id].PasswordHash);
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger<FixedAccountSeeder>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    /// <summary>Najmniejszy magazyn, jakiego wymaga <see cref="UserManager{TUser}"/> do założenia konta z hasłem.</summary>
    private sealed class MemoryUserStore : IUserPasswordStore<ApplicationUser>
    {
        public Dictionary<Guid, ApplicationUser> Accounts { get; } = [];

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct)
        {
            Accounts[user.Id] = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct) =>
            Task.FromResult(Accounts.GetValueOrDefault(Guid.Parse(userId)));

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
            Task.FromResult(Accounts.Values.FirstOrDefault(u => u.NormalizedUserName == normalizedUserName));

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken ct)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) =>
            Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken ct)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken ct)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash);

        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash is not null);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct)
        {
            Accounts.Remove(user.Id);
            return Task.FromResult(IdentityResult.Success);
        }

        public void Dispose()
        {
        }
    }
}
