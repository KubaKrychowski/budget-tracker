using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.EpisodicOrders.Commands;

/// <summary>Zakłada zlecenie epizodyczne — zaplanowane (z planem) albo od razu zrealizowane (ze wskazaną transakcją).</summary>
/// <remarks>
/// Zrealizowane NIE dostaje planu: kwotę, datę i kategorię niesie transakcja, a drugi zapis tych samych liczb
/// rozjechałby się przy pierwszej poprawce transakcji. Z tego samego powodu takie zlecenie nie ma dokąd „cofnąć się”.
///
/// Bez wskazanego budżetu zrealizowane trafia do budżetu SWOJEJ transakcji — lista transakcji pokazuje kilka budżetów
/// naraz, a budżet domyślny odrzuciłby transakcję z innego jako „nie z tego budżetu”.
/// </remarks>
public sealed class CreateEpisodicOrderCommandHandler(
    AppDbContext db,
    EpisodicOrdersBudgetScope scope,
    EpisodicOrderRequestValidator validator,
    EpisodicOrderTransactions transactions,
    ICurrentUserAccessor currentUser)
{
    public async Task<EpisodicOrderSavedResponseDto> HandleAsync(SaveEpisodicOrderRequestDto request, CancellationToken ct)
    {
        var (name, description) = EpisodicOrderRequestValidator.Texts(request);
        var budget = request is { BudgetId: null, TransactionId: { } fromList }
            ? await transactions.BudgetOfAsync(fromList, ct)
            : await scope.SingleAsync(request.BudgetId, ct);
        var order = new EpisodicOrder(budget, name, description, scope.Now(), currentUser.UserId);

        if (request.TransactionId is { } transactionId)
        {
            var transaction = await transactions.RequireUsableAsync(budget, transactionId, null, ct);
            order.Realize(transaction.BusinessId);
        }
        else
        {
            var (categoryId, amount, dueMonth) = await validator.PlanAsync(request, ct);
            order.Plan(categoryId, amount, dueMonth);
        }

        db.EpisodicOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return new EpisodicOrderSavedResponseDto(order.BusinessId);
    }
}
