namespace BudgetTracker.Identity.Services.Users;

/// <summary>Konto w skrócie — tyle, ile potrzebuje logika usuwania.</summary>
public sealed record AccountInfo(Guid Id, string Email, bool IsAdmin);

/// <summary>
/// Operacje na koncie w serwerze tożsamości, które składają się na jego usunięcie. Interfejs zamiast
/// <c>UserManager</c>, bo kolejność i zabezpieczenia w <see cref="UserDeletionService"/> mają być testowalne bez bazy.
/// </summary>
public interface IAccountStore
{
    Task<AccountInfo?> FindAsync(Guid id, CancellationToken ct);

    Task<int> CountAdminsAsync(CancellationToken ct);

    /// <summary>Blokuje logowanie na stałe i unieważnia istniejące sesje (nowy znacznik bezpieczeństwa). Powtarzalne.</summary>
    Task LockAsync(Guid id, CancellationToken ct);

    /// <summary>Usuwa tokeny i autoryzacje OpenIddict konta. Powtarzalne.</summary>
    Task RevokeSessionsAsync(Guid id, CancellationToken ct);

    /// <summary>Usuwa samo konto. Ostatni krok — po nim nie ma już czego ponawiać.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
