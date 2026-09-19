using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Services.Api;
using BudgetTracker.Identity.Services.Audit;

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
/// <para>
/// Każda próba, także odmowa i awaria, trafia do dziennika audytu <see cref="IAdminAuditLog"/> z identyfikatorem
/// wykonawcy. Zapis jest na samym końcu, PO operacji, i nie może jej cofnąć.
/// </para>
/// </remarks>
public sealed class UserDeletionService(
    IAccountStore accounts, IUserDataClient data, IAdminAuditLog audit, ILogger<UserDeletionService> logger)
{
    public Task<UserDeletionOutcome> DeleteByAdminAsync(Guid userId, Guid adminId, CancellationToken ct) =>
        DeleteAsync(userId, actorId: adminId, byAdmin: true, ct);

    public Task<UserDeletionOutcome> DeleteOwnAccountAsync(Guid userId, CancellationToken ct) =>
        DeleteAsync(userId, actorId: userId, byAdmin: false, ct);

    private async Task<UserDeletionOutcome> DeleteAsync(Guid userId, Guid actorId, bool byAdmin, CancellationToken ct)
    {
        var action = byAdmin ? AdminAuditAction.UserDeleted : AdminAuditAction.OwnAccountDeleted;

        if (byAdmin && userId == actorId)
        {
            return await FinishAsync(UserDeletionOutcome.CannotDeleteSelf, action, actorId, userId, email: null, rows: null, ct);
        }

        var account = await accounts.FindAsync(userId, ct);
        if (account is null)
        {
            return await FinishAsync(UserDeletionOutcome.NotFound, action, actorId, userId, email: null, rows: null, ct);
        }

        // Pilnuje też „Usuń moje konto": jedyny admin nie może zostawić serwera bez nikogo, kto zarządza kontami.
        if (account.IsAdmin && await accounts.CountAdminsAsync(ct) <= 1)
        {
            return await FinishAsync(UserDeletionOutcome.LastAdmin, action, actorId, userId, account.Email, rows: null, ct);
        }

        if (byAdmin) await accounts.LockAsync(userId, ct);

        int rows;
        try
        {
            rows = (await data.DeleteDataAsync(userId, ct)).Total;
            logger.LogInformation("Usunięto dane konta {UserId} ({Rows} wierszy), wykonał {ActorId}.", userId, rows, actorId);
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "Nie udało się usunąć danych konta {UserId} (próbował {ActorId}) — konto zostaje.", userId, actorId);
            return await FinishAsync(UserDeletionOutcome.DataServiceUnavailable, action, actorId, userId, account.Email, rows: null, ct);
        }

        // Ścieżka właściciela blokuje dopiero teraz (patrz uwagi wyżej); ścieżka admina zablokowała konto na początku.
        if (!byAdmin) await accounts.LockAsync(userId, ct);

        await accounts.RevokeSessionsAsync(userId, ct);
        await accounts.DeleteAsync(userId, ct);

        logger.LogInformation("Usunięto konto {UserId}, wykonał {ActorId}.", userId, actorId);
        return await FinishAsync(UserDeletionOutcome.Deleted, action, actorId, userId, account.Email, rows, ct);
    }

    private async Task<UserDeletionOutcome> FinishAsync(
        UserDeletionOutcome outcome, AdminAuditAction action, Guid actorId, Guid subjectId, string? email, int? rows, CancellationToken ct)
    {
        await audit.RecordAsync(new AdminAuditRecord(actorId, action, ToAuditOutcome(outcome), subjectId, email, Rows: rows), ct);
        return outcome;
    }

    private static AdminAuditOutcome ToAuditOutcome(UserDeletionOutcome outcome) => outcome switch
    {
        UserDeletionOutcome.Deleted => AdminAuditOutcome.Succeeded,
        UserDeletionOutcome.NotFound => AdminAuditOutcome.NotFound,
        UserDeletionOutcome.CannotDeleteSelf => AdminAuditOutcome.RefusedSelf,
        UserDeletionOutcome.LastAdmin => AdminAuditOutcome.RefusedLastAdmin,
        UserDeletionOutcome.DataServiceUnavailable => AdminAuditOutcome.DataServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
