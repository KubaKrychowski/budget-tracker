using BudgetTracker.Api.Features.Admin.Contracts;
using BudgetTracker.Api.Features.Admin.Services;

namespace BudgetTracker.Api.Features.Admin.Queries;

/// <summary>
/// Właściciele danych, których nie ma na liście znanych kont: dane sprzed wprowadzenia logowania (puste ID)
/// albo po koncie usuniętym poza aplikacją.
/// </summary>
public sealed class GetOrphanedDataQueryHandler(OwnerDataService ownerData)
{
    public async Task<OrphanedDataResponseDto> HandleAsync(OrphanedDataRequestDto request, CancellationToken ct)
    {
        var known = request.KnownUserIds.ToHashSet();

        var (counts, lastChanges) = await ownerData.AsSystemAsync(async () =>
            (await ownerData.CountByOwnerAsync(ct), await ownerData.LastChangeByOwnerAsync(ct)), ct);

        var owners = counts
            .Where(c => !known.Contains(c.Key))
            .OrderByDescending(c => c.Value.Total)
            .ThenBy(c => c.Key)
            .Select(c => new OrphanedOwnerResponseDto(
                c.Key, c.Value, lastChanges.TryGetValue(c.Key, out var last) ? last : null))
            .ToList();

        return new OrphanedDataResponseDto(owners);
    }
}
