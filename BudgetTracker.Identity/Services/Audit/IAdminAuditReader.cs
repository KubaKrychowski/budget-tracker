using BudgetTracker.Identity.Models;

namespace BudgetTracker.Identity.Services.Audit;

/// <summary>Odczyt dziennika audytu dla zakładki „Historia" w ekranach administratora. Tylko do odczytu.</summary>
public interface IAdminAuditReader
{
    Task<int> CountAsync(CancellationToken ct);

    /// <summary>Wpisy od najnowszego; <paramref name="page"/> liczone od 1.</summary>
    Task<IReadOnlyList<AdminAuditEntry>> PageAsync(int page, int pageSize, CancellationToken ct);
}
