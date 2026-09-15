namespace BudgetTracker.Api.Features.Cli.Exceptions;

/// <summary>
/// Brakujący albo niepoprawny argument komendy CLI (np. wymagana flaga, zła liczba). To błąd
/// SKŁADNI wywołania, nie reguły biznesowej — dispatcher łapie go sam i zwraca 400, zamiast
/// przepuszczać przez <c>DomainExceptionHandler</c>, który obsługuje wyłącznie wyjątki domenowe.
/// </summary>
public sealed class CliArgumentException(string message) : Exception(message);
