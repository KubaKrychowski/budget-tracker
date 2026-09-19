namespace BudgetTracker.Identity.Services.Users;

public enum UserDeletionOutcome
{
    /// <summary>Konto i jego dane zostały usunięte.</summary>
    Deleted = 1,

    /// <summary>Nie ma takiego konta (np. usunięte chwilę wcześniej).</summary>
    NotFound = 2,

    /// <summary>Administrator próbuje usunąć własne konto z ekranu administracyjnego — od tego jest „Usuń moje konto".</summary>
    CannotDeleteSelf = 3,

    /// <summary>To jedyny administrator; po jego usunięciu nikt nie zarządzałby kontami.</summary>
    LastAdmin = 4,

    /// <summary>API budżetu nie odpowiada. Konto NIE zostało usunięte; można ponowić.</summary>
    DataServiceUnavailable = 5,
}
