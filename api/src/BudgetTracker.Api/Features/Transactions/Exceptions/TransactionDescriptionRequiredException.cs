namespace BudgetTracker.Api.Features.Transactions.Exceptions;

/// <summary>
/// Opis transakcji jest wymagany — pusty zostawia w tabeli wiersz, którego nie da się ani
/// rozpoznać wzrokiem, ani znaleźć wyszukiwarką.
/// </summary>
public sealed class TransactionDescriptionRequiredException : Exception;
