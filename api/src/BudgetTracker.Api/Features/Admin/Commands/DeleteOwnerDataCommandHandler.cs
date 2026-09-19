using BudgetTracker.Api.Features.Admin.Contracts;
using BudgetTracker.Api.Features.Admin.Services;

namespace BudgetTracker.Api.Features.Admin.Commands;

/// <summary>
/// Trwale usuwa wszystkie dane właściciela. Idempotentne: powtórzenie na właścicielu bez danych zwraca same zera,
/// więc serwer tożsamości może bezpiecznie ponowić usuwanie konta po awarii w połowie.
/// </summary>
/// <remarks>
/// ⚠️ API nie zna kont, więc nie sprawdza, czy właściciel jeszcze istnieje w serwerze tożsamości — to obowiązek
/// wywołującego (Identity blokuje konto przed wywołaniem, a dla „danych bez właściciela" sprawdza, że konta nie ma).
/// </remarks>
public sealed class DeleteOwnerDataCommandHandler(OwnerDataService ownerData)
{
    public async Task<OwnerDataChangeResponseDto> HandleAsync(Guid ownerId, CancellationToken ct) =>
        new(await ownerData.AsSystemAsync(() => ownerData.DeleteAsync(ownerId, ct), ct));
}
