using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Dashboard.Contracts;

/// <summary>Wiersz tabeli „Ostatnie transakcje" na dashboardzie.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> transakcji.</param>
/// <param name="Status">Nazwa <c>TransactionStatus</c> — ten sam format co na liście transakcji.</param>
public record RecentTransactionResponseDto(
    Guid Id, DateOnly Date, string Description, decimal Amount,
    string? CategoryName, string Status);
