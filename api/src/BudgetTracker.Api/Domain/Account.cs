using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Konto albo karta, z której pochodzą transakcje.</summary>
/// <remarks>
/// ⚠️ Konto nie bierze dziś udziału w imporcie — <c>Transaction.AccountId</c> istnieje, ale
/// <c>CommitImportCommandHandler</c> go nie wypełnia (krok 1 steppera pyta o bank i budżet,
/// nie o konto). Dopóki to się nie zmieni, konta pochodzą wyłącznie z seeda.
/// </remarks>
public class Account(string name, AccountType type) : Entity
{
    public string Name { get; protected set; } = name;

    /// <summary>Typ konta — do bazy trafia kodem ze słownika <c>AccountTypes</c>.</summary>
    public AccountType Type { get; protected set; } = type;
}
