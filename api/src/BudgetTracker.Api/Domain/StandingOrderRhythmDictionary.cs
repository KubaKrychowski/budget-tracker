using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Słownik rytmów zleceń stałych (tabela <c>StandingOrderRhythms</c>) — po jednym wierszu na wartość <see cref="StandingOrderRhythm"/>.</summary>
public class StandingOrderRhythmDictionary(StandingOrderRhythm code) : DictionaryEntity<StandingOrderRhythm>(code);
