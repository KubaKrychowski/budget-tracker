namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Wynik symulacji strategii.</summary>
/// <param name="Months">Seria miesięcy od początku strategii przez cały horyzont.</param>
/// <param name="LoanPaidOffIn">Pierwszy miesiąc z zerowym długiem po tym, jak kredyt istniał; <c>null</c> = nie spłacony lub brak kredytu.</param>
/// <param name="CushionReachedIn">Pierwszy miesiąc, w którym gotówka osiągnęła poduszkę przy zerowym długu; <c>null</c> = nie osiągnięta.</param>
/// <param name="FinalCash">Gotówka na koniec horyzontu.</param>
/// <param name="TotalInterest">Suma odsetek w całym horyzoncie.</param>
/// <param name="Nodes">Wynik per węzeł — tylko węzły, które się wykonały.</param>
/// <param name="Problems">Problemy grafu (ta sama lista przy każdym uruchomieniu).</param>
public sealed record StrategyResult(
    IReadOnlyList<StrategyMonthPoint> Months,
    DateOnly? LoanPaidOffIn,
    DateOnly? CushionReachedIn,
    decimal FinalCash,
    decimal TotalInterest,
    IReadOnlyList<StrategyNodeOutcome> Nodes,
    IReadOnlyList<StrategyProblem> Problems);
