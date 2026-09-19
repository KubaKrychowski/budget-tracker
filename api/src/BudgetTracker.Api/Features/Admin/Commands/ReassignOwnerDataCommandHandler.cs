using BudgetTracker.Api.Features.Admin.Contracts;
using BudgetTracker.Api.Features.Admin.Exceptions;
using BudgetTracker.Api.Features.Admin.Services;

namespace BudgetTracker.Api.Features.Admin.Commands;

/// <summary>Przepisuje wszystkie dane właściciela na inne konto. Nic nie kasuje, więc jest odwracalne.</summary>
public sealed class ReassignOwnerDataCommandHandler(OwnerDataService ownerData)
{
    public async Task<OwnerDataChangeResponseDto> HandleAsync(
        Guid ownerId, ReassignOwnerRequestDto request, CancellationToken ct)
    {
        // Puste ID jako cel zrobiłoby z danych znowu „dane sprzed logowania", niewidoczne dla nikogo.
        if (request.TargetUserId == Guid.Empty || request.TargetUserId == ownerId)
        {
            throw new OwnerReassignInvalidException(ownerId, request.TargetUserId);
        }

        return new(await ownerData.AsSystemAsync(
            () => ownerData.ReassignAsync(ownerId, request.TargetUserId, ct), ct));
    }
}
