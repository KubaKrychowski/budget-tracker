namespace BudgetTracker.Identity.Models;

/// <summary>Jak skończyła się próba. Odmowy i awarie są w dzienniku tak samo jak sukcesy — ślad po próbie też jest śladem.</summary>
public enum AdminAuditOutcome
{
    Succeeded = 1,

    /// <summary>Konta albo danych już nie było.</summary>
    NotFound = 2,

    /// <summary>Administrator próbował usunąć siebie z ekranu administracyjnego.</summary>
    RefusedSelf = 3,

    /// <summary>Próba usunięcia jedynego administratora.</summary>
    RefusedLastAdmin = 4,

    /// <summary>API budżetu nie odpowiedziało; nic (poza ewentualną blokadą konta) nie zostało zmienione.</summary>
    DataServiceUnavailable = 5,
}
