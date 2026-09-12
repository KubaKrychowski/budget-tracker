namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>
/// Wskazana transakcja nie nadaje się na rozliczenie: nie należy do tego budżetu, nie ma
/// kategorii „Oszczędności", nie jest wypłatą (kwota dodatnia) albo zamyka już inną rezerwację.
/// </summary>
/// <remarks>
/// ⚠️ Bez tej walidacji „rozliczenie" zamykałoby rezerwację dowolnym wierszem z wyciągu,
/// a jedna wypłata mogłaby zamknąć kilka kopert naraz — i nikt by tego nie zauważył.
/// </remarks>
public sealed class SettlementTransactionInvalidException : Exception;
