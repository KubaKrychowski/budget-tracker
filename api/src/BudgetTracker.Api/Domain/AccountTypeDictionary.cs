using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Słownik typów kont (tabela <c>AccountTypes</c>) — po jednym wierszu na wartość <see cref="AccountType"/>.</summary>
public class AccountTypeDictionary(AccountType code) : DictionaryEntity<AccountType>(code);
