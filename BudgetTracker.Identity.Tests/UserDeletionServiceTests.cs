using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Services.Api;
using BudgetTracker.Identity.Services.Audit;
using BudgetTracker.Identity.Services.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Usuwanie konta rozciąga się na dwie bazy (Identity i API), więc kolejność kroków i zabezpieczenia to cała reguła.
/// Testy pilnują, co zostaje po awarii w połowie i że nikt nie skasuje siebie ani ostatniego admina.
/// </summary>
public sealed class UserDeletionServiceTests
{
    private static readonly Guid Admin = Guid.CreateVersion7();
    private static readonly Guid Other = Guid.CreateVersion7();
    private static readonly Guid SecondAdmin = Guid.CreateVersion7();

    private readonly List<string> calls = [];
    private readonly FakeAccounts accounts;
    private readonly FakeData data;
    private readonly FakeAudit audit = new();
    private readonly UserDeletionService service;

    public UserDeletionServiceTests()
    {
        accounts = new FakeAccounts(calls);
        data = new FakeData(calls);
        service = new UserDeletionService(accounts, data, audit, NullLogger<UserDeletionService>.Instance);

        accounts.Accounts[Admin] = new AccountInfo(Admin, "admin@example.com", IsAdmin: true);
        accounts.Accounts[Other] = new AccountInfo(Other, "anna@example.com", IsAdmin: false);
    }

    [Fact]
    public async Task Admin_deletion_locks_the_account_first_then_deletes_data_then_the_account()
    {
        var outcome = await service.DeleteByAdminAsync(Other, Admin, default);

        Assert.Equal(UserDeletionOutcome.Deleted, outcome);
        // Blokada PIERWSZA: użytkownik nie może w trakcie dokładać danych, których kasowanie już przeszło.
        Assert.Equal(["find", "lock", "data", "revoke", "delete"], calls);
    }

    [Fact]
    public async Task Own_account_deletion_removes_data_before_locking_so_a_failure_does_not_lock_the_user_out()
    {
        var outcome = await service.DeleteOwnAccountAsync(Other, default);

        Assert.Equal(UserDeletionOutcome.Deleted, outcome);
        Assert.Equal(["find", "data", "lock", "revoke", "delete"], calls);
    }

    [Fact]
    public async Task When_the_data_service_fails_an_admin_deletion_leaves_the_account_locked_but_not_deleted()
    {
        data.Fail = true;

        var outcome = await service.DeleteByAdminAsync(Other, Admin, default);

        Assert.Equal(UserDeletionOutcome.DataServiceUnavailable, outcome);
        // Konto zablokowane, ale NIE skasowane: skasowane przed danymi zostawiłoby dane, których nikt nie usunie zwykłą drogą.
        Assert.Equal(["find", "lock", "data"], calls);
        Assert.Contains(Other, accounts.Accounts.Keys);
    }

    [Fact]
    public async Task When_the_data_service_fails_own_account_deletion_changes_nothing()
    {
        data.Fail = true;

        var outcome = await service.DeleteOwnAccountAsync(Other, default);

        Assert.Equal(UserDeletionOutcome.DataServiceUnavailable, outcome);
        Assert.Equal(["find", "data"], calls);
    }

    [Fact]
    public async Task A_failed_deletion_can_be_repeated_and_then_completes()
    {
        data.Fail = true;
        await service.DeleteByAdminAsync(Other, Admin, default);

        data.Fail = false;
        calls.Clear();
        var outcome = await service.DeleteByAdminAsync(Other, Admin, default);

        Assert.Equal(UserDeletionOutcome.Deleted, outcome);
        Assert.DoesNotContain(Other, accounts.Accounts.Keys);
    }

    [Fact]
    public async Task An_admin_cannot_delete_their_own_account_from_the_admin_screen()
    {
        var outcome = await service.DeleteByAdminAsync(Admin, Admin, default);

        Assert.Equal(UserDeletionOutcome.CannotDeleteSelf, outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task The_only_administrator_cannot_be_deleted_by_an_admin_or_by_themselves()
    {
        accounts.Accounts[Other] = new AccountInfo(Other, "anna@example.com", IsAdmin: false);
        // Admin jest jedyny; próba usunięcia go przez KOGOKOLWIEK innego (tu: Other udaje admina) też ma się nie udać.
        var byOther = await service.DeleteByAdminAsync(Admin, Other, default);
        var byHimself = await service.DeleteOwnAccountAsync(Admin, default);

        Assert.Equal(UserDeletionOutcome.LastAdmin, byOther);
        Assert.Equal(UserDeletionOutcome.LastAdmin, byHimself);
        Assert.DoesNotContain("lock", calls);
        Assert.DoesNotContain("data", calls);
        Assert.Contains(Admin, accounts.Accounts.Keys);
    }

    [Fact]
    public async Task One_of_two_administrators_can_be_deleted()
    {
        accounts.Accounts[SecondAdmin] = new AccountInfo(SecondAdmin, "second@example.com", IsAdmin: true);

        var outcome = await service.DeleteByAdminAsync(SecondAdmin, Admin, default);

        Assert.Equal(UserDeletionOutcome.Deleted, outcome);
    }

    [Fact]
    public async Task An_unknown_account_is_reported_without_touching_anything()
    {
        var outcome = await service.DeleteByAdminAsync(Guid.CreateVersion7(), Admin, default);

        Assert.Equal(UserDeletionOutcome.NotFound, outcome);
        Assert.Equal(["find"], calls);
    }

    [Fact]
    public async Task A_completed_admin_deletion_is_audited_with_the_actor_the_subject_and_the_row_count()
    {
        data.Rows = 42;

        await service.DeleteByAdminAsync(Other, Admin, default);

        var entry = Assert.Single(audit.Records);
        Assert.Equal(AdminAuditAction.UserDeleted, entry.Action);
        Assert.Equal(AdminAuditOutcome.Succeeded, entry.Outcome);
        Assert.Equal(Admin, entry.ActorId);
        Assert.Equal(Other, entry.SubjectId);
        Assert.Equal("anna@example.com", entry.SubjectEmail);
        Assert.Equal(42, entry.Rows);
    }

    [Fact]
    public async Task Own_account_deletion_is_audited_with_the_user_as_their_own_actor()
    {
        await service.DeleteOwnAccountAsync(Other, default);

        var entry = Assert.Single(audit.Records);
        Assert.Equal(AdminAuditAction.OwnAccountDeleted, entry.Action);
        Assert.Equal(Other, entry.ActorId);
        Assert.Equal(Other, entry.SubjectId);
    }

    [Fact]
    public async Task Refusals_and_failures_are_audited_too()
    {
        data.Fail = true;
        await service.DeleteByAdminAsync(Other, Admin, default);
        await service.DeleteByAdminAsync(Admin, Admin, default);
        await service.DeleteOwnAccountAsync(Admin, default);
        await service.DeleteByAdminAsync(Guid.CreateVersion7(), Admin, default);

        Assert.Equal(
            [
                AdminAuditOutcome.DataServiceUnavailable,
                AdminAuditOutcome.RefusedSelf,
                AdminAuditOutcome.RefusedLastAdmin,
                AdminAuditOutcome.NotFound,
            ],
            audit.Records.Select(r => r.Outcome));
        // Nieudane usunięcie nie może zostawić śladu liczby wierszy, których nie skasowano.
        Assert.All(audit.Records, r => Assert.Null(r.Rows));
    }

    private sealed class FakeAudit : IAdminAuditLog
    {
        public List<AdminAuditRecord> Records { get; } = [];

        public Task RecordAsync(AdminAuditRecord record, CancellationToken ct)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccounts(List<string> calls) : IAccountStore
    {
        public Dictionary<Guid, AccountInfo> Accounts { get; } = [];

        public Task<AccountInfo?> FindAsync(Guid id, CancellationToken ct)
        {
            calls.Add("find");
            return Task.FromResult(Accounts.GetValueOrDefault(id));
        }

        public Task<int> CountAdminsAsync(CancellationToken ct) => Task.FromResult(Accounts.Values.Count(a => a.IsAdmin));

        public Task LockAsync(Guid id, CancellationToken ct)
        {
            calls.Add("lock");
            return Task.CompletedTask;
        }

        public Task RevokeSessionsAsync(Guid id, CancellationToken ct)
        {
            calls.Add("revoke");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct)
        {
            calls.Add("delete");
            Accounts.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeData(List<string> calls) : IUserDataClient
    {
        public bool Fail { get; set; }

        public int Rows { get; set; }

        public Task<IReadOnlyDictionary<Guid, OwnerDataCounts>> GetSummariesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OrphanedOwner>> GetOrphansAsync(IReadOnlyCollection<Guid> knownUserIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<OwnerDataCounts> DeleteDataAsync(Guid ownerId, CancellationToken ct)
        {
            calls.Add("data");
            return Fail
                ? throw new UserDataServiceException("API nie odpowiada")
                : Task.FromResult(OwnerDataCounts.Empty with { Transactions = Rows });
        }

        public Task<OwnerDataCounts> ReassignAsync(Guid ownerId, Guid targetUserId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CreateUserContainerAsync(Guid userId, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
