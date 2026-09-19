namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <param name="OwnerId">Identyfikator właściciela bez konta; <c>Guid.Empty</c> to dane sprzed wprowadzenia logowania.</param>
/// <param name="LastChangedAt">Najnowszy znacznik czasu z budżetów i transakcji tego właściciela; <c>null</c>, gdy nie ma żadnego.</param>
public sealed record OrphanedOwnerResponseDto(
    Guid OwnerId, OwnerDataCountsResponseDto Counts, DateTimeOffset? LastChangedAt);
