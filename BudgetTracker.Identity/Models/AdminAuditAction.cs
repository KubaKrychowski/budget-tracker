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
}
