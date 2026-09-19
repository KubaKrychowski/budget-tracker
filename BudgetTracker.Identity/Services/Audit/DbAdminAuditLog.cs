using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Models;

namespace BudgetTracker.Identity.Services.Audit;

public sealed class DbAdminAuditLog(
    ApplicationDbContext db, TimeProvider clock, ILogger<DbAdminAuditLog> logger) : IAdminAuditLog
{
    public async Task RecordAsync(AdminAuditRecord record, CancellationToken ct)
    {
        try
        {
            db.AdminAudit.Add(new AdminAuditEntry
            {
                OccurredAt = clock.GetUtcNow(),
                ActorId = record.ActorId,
                Action = record.Action,
                Outcome = record.Outcome,
                SubjectId = record.SubjectId,
                SubjectEmailHash = AuditEmailHash.Compute(record.SubjectEmail),
                TargetId = record.TargetId,
                Rows = record.Rows,
            });

            // CancellationToken.None: żądanie mogło zostać przerwane PO wykonaniu operacji, a ślad ma zostać.
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Nie udało się zapisać wpisu audytu ({Action}, {Outcome}, wykonał {ActorId}, dotyczy {SubjectId}).",
                record.Action, record.Outcome, record.ActorId, record.SubjectId);
        }
    }
}
