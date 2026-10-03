namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Wynik symulacji strategii.</summary>
/// <param name="Months">Seria miesięcy od początku strategii przez cały horyzont.</param>
/// <param name="LoanPaidOffIn">Miesiąc spłaty kredytu; <c>null</c> = nie spłacony albo brak kredytu.</param>
/// <param name="CushionReachedIn">Miesiąc osiągnięcia poduszki przy zerowym długu; <c>null</c> = nie osiągnięta.</param>
/// <param name="FinalCash">Gotówka na koniec horyzontu.</param>
/// <param name="TotalInterest">Suma odsetek w całym horyzoncie.</param>
/// <param name="Nodes">Wynik per węzeł, tylko dla węzłów, które się wykonały.</param>
/// <param name="Problems">Problemy grafu do pokazania na węzłach.</param>
public sealed record StrategyResultResponseDto(
    IReadOnlyList<StrategyMonthResponseDto> Months,
    DateOnly? LoanPaidOffIn,
    DateOnly? CushionReachedIn,
    decimal FinalCash,
    decimal TotalInterest,
    IReadOnlyList<StrategyNodeOutcomeResponseDto> Nodes,
    IReadOnlyList<StrategyProblemResponseDto> Problems);
