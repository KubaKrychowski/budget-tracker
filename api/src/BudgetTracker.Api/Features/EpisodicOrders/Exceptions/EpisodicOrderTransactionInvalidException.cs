namespace BudgetTracker.Api.Features.EpisodicOrders.Exceptions;

/// <summary>
/// Wskazana transakcja nie może zrealizować zlecenia: nie istnieje, jest z innego budżetu, nie jest wydatkiem albo
/// należy już do innego zlecenia epizodycznego.
/// </summary>
/// <remarks>
/// 400, nie 404, także dla nieznanej transakcji — identyfikator przychodzi w CIELE żądania, więc 404 mówiłby
/// „nie ma takiego zlecenia”. Ta sama zasada co przy rozliczaniu rezerwacji.
/// </remarks>
public sealed class EpisodicOrderTransactionInvalidException : Exception;
