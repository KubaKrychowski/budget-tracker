using BudgetTracker.Api.Features.Admin.Contracts;
using BudgetTracker.Api.Features.Admin.Services;

namespace BudgetTracker.Api.Features.Admin.Queries;

/// <summary>Ilość danych wskazanych kont — kolumna „Dane w aplikacji" na liście użytkowników w serwerze tożsamości.</summary>
public sealed class GetUserDataSummariesQueryHandler(OwnerDataService ownerData)
{
    public async Task<UserDataSummariesResponseDto> HandleAsync(UserDataSummaryRequestDto request, CancellationToken ct)
    {
        var counts = await ownerData.AsSystemAsync(() => ownerData.CountByOwnerAsync(ct), ct);

        var users = request.UserIds
            .Distinct()
            .Select(id => new UserDataSummaryResponseDto(id, counts.GetValueOrDefault(id, OwnerDataCountsResponseDto.Empty)))
            .ToList();

        return new UserDataSummariesResponseDto(users);
    }
}
