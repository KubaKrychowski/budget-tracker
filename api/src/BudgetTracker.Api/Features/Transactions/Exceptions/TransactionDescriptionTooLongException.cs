using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;

namespace BudgetTracker.Api.Features.Transactions.Exceptions;

/// <summary>
/// Opis dłuższy niż <see cref="TransactionLimits.DescriptionMaxLength"/>.
/// </summary>
/// <remarks>
/// Osobny wyjątek, a nie poleganie na błędzie bazy: <c>DbUpdateException</c> z <c>varchar(500)</c> nie jest
/// mapowany na żaden kod HTTP, więc wklejenie za długiego tekstu dawałoby 500.
/// </remarks>
public sealed class TransactionDescriptionTooLongException : Exception;
