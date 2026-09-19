namespace BudgetTracker.Identity.Services.Api;

/// <summary>Klient endpointów <c>/api/admin</c> API budżetu — dane użytkowników trzyma API, konta serwer tożsamości.</summary>
/// <exception cref="UserDataServiceException">Każda metoda, gdy API jest niedostępne albo odrzuci żądanie.</exception>
public interface IUserDataClient
{
    /// <summary>Ilość danych wskazanych kont; konta bez danych mają same zera.</summary>
    Task<IReadOnlyDictionary<Guid, OwnerDataCounts>> GetSummariesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Właściciele danych, których nie ma na liście <paramref name="knownUserIds"/>.</summary>
    Task<IReadOnlyList<OrphanedOwner>> GetOrphansAsync(IReadOnlyCollection<Guid> knownUserIds, CancellationToken ct);

    /// <summary>Trwale usuwa wszystkie dane właściciela; powtórzenie jest bezpieczne (zwraca zera).</summary>
    Task<OwnerDataCounts> DeleteDataAsync(Guid ownerId, CancellationToken ct);

    /// <summary>Przepisuje dane właściciela na inne konto, niczego nie kasując.</summary>
    Task<OwnerDataCounts> ReassignAsync(Guid ownerId, Guid targetUserId, CancellationToken ct);
}
