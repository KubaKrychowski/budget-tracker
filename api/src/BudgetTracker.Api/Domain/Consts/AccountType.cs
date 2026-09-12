namespace BudgetTracker.Api.Domain.Consts;

/// <summary>
/// Typ konta. Do bazy trafia NAZWA wartości — kod słownika <see cref="AccountTypeDictionary"/>;
/// zmiana nazwy wymaga migracji danych.
/// </summary>
public enum AccountType
{
    Bank = 1,

    /// <summary>Karta lunchowa od pracodawcy (Pluxee, Edenred itp.).</summary>
    LunchCard = 2,

    Cash = 3
}
