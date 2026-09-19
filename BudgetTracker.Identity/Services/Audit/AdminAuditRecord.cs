using BudgetTracker.Identity.Models;

namespace BudgetTracker.Identity.Services.Audit;

/// <summary>Zgłoszenie do dziennika audytu. Adres e-mail podaje się jawnie — do dziennika trafia dopiero jego skrót.</summary>
public sealed record AdminAuditRecord(
    Guid ActorId,
    AdminAuditAction Action,
    AdminAuditOutcome Outcome,
    Guid SubjectId,
    string? SubjectEmail = null,
    Guid? TargetId = null,
    int? Rows = null);
