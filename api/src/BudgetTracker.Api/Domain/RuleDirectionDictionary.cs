using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Słownik stron przepływu reguł (tabela <c>RuleDirections</c>) — po jednym wierszu na wartość <see cref="RuleDirection"/>.</summary>
public class RuleDirectionDictionary(RuleDirection code) : DictionaryEntity<RuleDirection>(code);
