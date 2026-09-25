namespace BudgetTracker.Identity.Models;

/// <summary>Co zostało zrobione. Zapisywane w bazie jako nazwa, więc zmiana nazwy członka wymaga migracji danych.</summary>
public enum AdminAuditAction
{
    /// <summary>Administrator usunął cudze konto razem z jego danymi.</summary>
    UserDeleted = 1,

    /// <summary>Użytkownik usunął własne konto („Usuń moje konto").</summary>
    OwnAccountDeleted = 2,

    /// <summary>Administrator przepisał dane bez właściciela na konto.</summary>
    OrphanDataAssigned = 3,

    /// <summary>Administrator usunął dane bez właściciela.</summary>
    OrphanDataDeleted = 4,

    /// <summary>Administrator dopisał adres do listy zaproszeń do zamkniętej bety.</summary>
    BetaInviteAdded = 5,

    /// <summary>
    /// Administrator usunął adres z listy zaproszeń. ⚠️ To NIE jest usunięcie konta: jeśli zaproszony zdążył się
    /// zarejestrować, konto zostaje i dalej się loguje. Skasowanie konta ma własny wpis (<see cref="UserDeleted"/>).
    /// </summary>
    BetaInviteRemoved = 6,
}
