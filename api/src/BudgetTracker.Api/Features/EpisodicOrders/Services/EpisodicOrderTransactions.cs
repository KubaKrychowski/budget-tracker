using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.EpisodicOrders.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.EpisodicOrders.Services;

/// <summary>Które transakcje mogą zrealizować zlecenie epizodyczne — wspólne dla zapisu i listy kandydatów.</summary>
/// <remarks>
/// Jeden warunek w jednym miejscu: lista kandydatów pokazująca wiersz, którego zapis potem nie przyjmie, byłaby
/// ślepym zaułkiem w dialogu.
/// </remarks>
public sealed class EpisodicOrderTransactions(AppDbContext db)
{
    /// <summary>
    /// Wydatki budżetu, które nie należą do innego zlecenia epizodycznego (<paramref name="exceptOrderId"/> — samo
    /// zlecenie, żeby dało się wskazać tę samą transakcję ponownie).
    /// </summary>
    public IQueryable<Transaction> Usable(Guid budgetBusinessId, Guid? exceptOrderId) =>
        db.Transactions.Where(t =>
            t.BudgetBusinessId == budgetBusinessId
            && t.Amount < 0
            && !db.EpisodicOrders.Any(o => o.TransactionBusinessId == t.BusinessId && o.BusinessId != exceptOrderId));

    /// <summary>Budżet transakcji; nieznana albo bez budżetu to 400, jak każda nienadająca się transakcja.</summary>
    public async Task<Guid> BudgetOfAsync(Guid transactionId, CancellationToken ct) =>
        await db.Transactions
            .Where(t => t.BusinessId == transactionId)
            .Select(t => t.BudgetBusinessId)
            .FirstOrDefaultAsync(ct)
        ?? throw new EpisodicOrderTransactionInvalidException();

    /// <summary>Sprawdza wskazaną transakcję; nienadająca się to 400.</summary>
    public async Task<Transaction> RequireUsableAsync(
        Guid budgetBusinessId, Guid transactionId, Guid? exceptOrderId, CancellationToken ct) =>
        await Usable(budgetBusinessId, exceptOrderId).FirstOrDefaultAsync(t => t.BusinessId == transactionId, ct)
        ?? throw new EpisodicOrderTransactionInvalidException();
}
