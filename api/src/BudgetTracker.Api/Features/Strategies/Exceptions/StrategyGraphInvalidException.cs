namespace BudgetTracker.Api.Features.Strategies.Exceptions;

/// <summary>
/// Graf jest STRUKTURALNIE niepoprawny: powtórzony identyfikator, połączenie do nieistniejącego węzła, połączenie węzła z
/// samym sobą, etykieta tak/nie poza warunkiem, zbyt wiele elementów albo wartość spoza zakresu.
/// </summary>
/// <remarks>
/// To coś innego niż problem grafu (<c>StrategyProblem</c>): problemy są informacją i szkic z nimi zapisuje się zawsze,
/// a strukturalnego błędu nie da się zapisać w formacie, który klient potrafi narysować.
/// </remarks>
public sealed class StrategyGraphInvalidException : Exception;
