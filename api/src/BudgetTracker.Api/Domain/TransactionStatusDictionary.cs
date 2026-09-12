using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Słownik statusów transakcji (tabela <c>TransactionStatuses</c>) — po jednym wierszu na wartość <see cref="TransactionStatus"/>.</summary>
public class TransactionStatusDictionary(TransactionStatus code) : DictionaryEntity<TransactionStatus>(code);
