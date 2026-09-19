namespace BudgetTracker.Identity.Services.Audit;

/// <summary>Dziennik audytu operacji administracyjnych i usuwania kont.</summary>
public interface IAdminAuditLog
{
    /// <summary>
    /// Zapisuje wpis. Nie rzuca wyjątku przy awarii zapisu: dziennik jest wołany PO operacji, a błąd audytu nie może jej
    /// cofnąć ani zamienić udanego usunięcia w komunikat o błędzie. Awaria idzie do logu jako błąd.
    /// </summary>
    Task RecordAsync(AdminAuditRecord record, CancellationToken ct);
}
