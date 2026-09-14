using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Wiersz tabeli listy transakcji.</summary>
/// <param name="Status">
/// Tekst z <c>TransactionStatus.ToString()</c> — tak samo jak <c>RecentTransactionResponseDto.Status</c>
/// na dashboardzie, żeby front miał jeden format statusu do tłumaczenia w obu miejscach.
/// </param>
/// <param name="EpisodicOrderName">
/// Zlecenie epizodyczne, które ta transakcja zrealizowała — kolumna „Epizodyczne” (dawniej flaga „duży wydatek”).
/// </param>
public sealed record TransactionListItemResponseDto(
    Guid Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    Guid? CategoryId,
    string? CategoryName,
    string Status,
    Guid? EpisodicOrderId,
    string? EpisodicOrderName,
    decimal? Confidence);
