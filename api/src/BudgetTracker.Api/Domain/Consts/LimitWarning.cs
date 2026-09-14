namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Stałe progu ostrzeżenia limitu (<see cref="BudgetItem.WarningThreshold"/>).</summary>
public static class LimitWarning
{
    /// <summary>Próg, który dostaje limit bez jawnie podanego — i każdy limit sprzed wprowadzenia progu.</summary>
    public const int DefaultThreshold = 80;

    /// <summary>Najniższy sensowny próg: 0% ostrzegałoby od pierwszej złotówki, czyli zawsze.</summary>
    public const int MinThreshold = 1;

    /// <summary>Najwyższy próg: powyżej 100% „ostrzeżenie" przychodziłoby już po przekroczeniu limitu.</summary>
    public const int MaxThreshold = 100;
}
