using BudgetTracker.Identity.Services.Api;

namespace BudgetTracker.Identity.Services.Users;

/// <summary>
/// Trwałe usunięcie konta razem z jego danymi. Konta leżą w bazie serwera tożsamości, dane w bazie API budżetu,
/// więc nie da się tego zrobić jedną transakcją — kolejność kroków decyduje, co zostaje po awarii w połowie.
/// </summary>
/// <remarks>
/// <para>
/// <b>Usunięcie przez administratora</b>: konto blokujemy PIERWSZE (użytkownik mógłby w trakcie dokładać dane),
/// potem kasujemy dane w API, potem sesje, na końcu konto. Awaria API zostawia konto ZABLOKOWANE i nietknięte —
/// administrator ponawia usunięcie, a każdy krok jest powtarzalny.
/// </para>
/// <para>
/// <b>„Usuń moje konto"</b>: dane kasujemy PRZED blokadą. Zablokowany właściciel nie mógłby się zalogować, żeby
/// ponowić próbę, gdyby API akurat nie działało — więc przy awarii nic się nie zmienia i użytkownik po prostu
/// próbuje później.
/// </para>
/// <para>
/// ⚠️ Nigdy odwrotnie: konto skasowane przed danymi zostawia dane, których nikt już nie może usunąć zwykłą drogą
/// (stąd zakładka „Dane bez właściciela").
/// </para>
/// </remarks>
public sealed class UserDeletionService(
    IAccountStore accounts, IUserDataClient data, ILogger<UserDeletionService> logger)
{
    public Task<UserDeletionOutcome> DeleteByAdminAsync(Guid userId, Guid adminId, CancellationToken ct) =>
        userId == adminId
            ? Task.FromResult(UserDeletionOutcome.CannotDeleteSelf)
            : DeleteAsync(userId, lockFirst: true, ct);

    public Task<UserDeletionOutcome> DeleteOwnAccountAsync(Guid userId, CancellationToken ct) =>
        DeleteAsync(userId, lockFirst: false, ct);

    private async Task<UserDeletionOutcome> DeleteAsync(Guid userId, bool lockFirst, CancellationToken ct)
    {
        var account = await accounts.FindAsync(userId, ct);
        if (account is null) return UserDeletionOutcome.NotFound;

        // Pilnuje też „Usuń moje konto": jedyny admin nie może zostawić serwera bez nikogo, kto zarządza kontami.
        if (account.IsAdmin && await accounts.CountAdminsAsync(ct) <= 1) return UserDeletionOutcome.LastAdmin;

        if (lockFirst) await accounts.LockAsync(userId, ct);

        try
        {
            var deleted = await data.DeleteDataAsync(userId, ct);
            logger.LogInformation("Usunięto dane konta {UserId}: {Rows} wierszy.", userId, deleted.Total);
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "Nie udało się usunąć danych konta {UserId} — konto zostaje.", userId);
            return UserDeletionOutcome.DataServiceUnavailable;
        }

        // Ścieżka właściciela blokuje dopiero teraz (patrz uwagi wyżej); ścieżka admina zablokowała konto na początku.
        if (!lockFirst) await accounts.LockAsync(userId, ct);

        await accounts.RevokeSessionsAsync(userId, ct);
        await accounts.DeleteAsync(userId, ct);

        logger.LogInformation("Usunięto konto {UserId}.", userId);
        return UserDeletionOutcome.Deleted;
    }
}
