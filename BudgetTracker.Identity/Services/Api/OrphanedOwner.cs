namespace BudgetTracker.Identity.Services.Api;

/// <summary>Dane bez właściciela: identyfikator (puste ID = sprzed logowania), ich ilość i najnowszy znacznik czasu.</summary>
public sealed record OrphanedOwner(Guid OwnerId, OwnerDataCounts Counts, DateTimeOffset? LastChangedAt);
