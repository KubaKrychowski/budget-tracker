using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Identity.Services.Audit;

public sealed class DbAdminAuditReader(ApplicationDbContext db) : IAdminAuditReader
{
    public Task<int> CountAsync(CancellationToken ct) => db.AdminAudit.CountAsync(ct);

    public async Task<IReadOnlyList<AdminAuditEntry>> PageAsync(int page, int pageSize, CancellationToken ct) =>
        await db.AdminAudit
            .AsNoTracking()
            // Id (UUID v7) rozstrzyga wpisy z tej samej chwili tak samo przy każdym odczycie, żeby strony się nie nakładały.
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
}
