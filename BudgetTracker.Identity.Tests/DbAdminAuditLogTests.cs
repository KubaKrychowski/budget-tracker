using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Dziennik audytu jest zapisywany PO operacji, której nie da się już cofnąć (konto skasowane, dane usunięte).
/// Awaria zapisu nie może więc zamienić udanego usunięcia w błąd 500 — pominięty wpis ma trafić do logów.
/// </summary>
public sealed class DbAdminAuditLogTests
{
    [Fact]
    public async Task A_failed_audit_write_is_logged_and_does_not_throw()
    {
        // Port, na którym nic nie słucha: zapis kończy się natychmiast błędem połączenia.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2;Command Timeout=2")
            .Options;
        await using var db = new ApplicationDbContext(options);
        var audit = new DbAdminAuditLog(db, TimeProvider.System, NullLogger<DbAdminAuditLog>.Instance);

        var record = new AdminAuditRecord(
            Guid.CreateVersion7(), AdminAuditAction.UserDeleted, AdminAuditOutcome.Succeeded, Guid.CreateVersion7(), "anna@example.com", Rows: 3);

        var ex = await Record.ExceptionAsync(() => audit.RecordAsync(record, default));

        Assert.Null(ex);
    }
}
