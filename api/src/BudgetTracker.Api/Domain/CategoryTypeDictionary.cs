using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Słownik typów kategorii (tabela <c>CategoryTypes</c>) — po jednym wierszu na wartość <see cref="CategoryType"/>.</summary>
public class CategoryTypeDictionary(CategoryType code) : DictionaryEntity<CategoryType>(code);
