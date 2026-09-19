using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.StandingOrders.Commands;

/// <summary>Zakłada zlecenie stałe i od razu przypina do niego pasujące transakcje z historii budżetu.</summary>
public sealed class CreateStandingOrderCommandHandler(
    AppDbContext db, StandingOrdersBudgetScope scope, StandingOrderMatcher matcher, ICurrentUserAccessor currentUser)
{
    /// <summary>Zapis zlecenia i przypięcia wstecz w JEDNEJ transakcji bazodanowej.</summary>
    /// <remarks>
    /// Zlecenie zapisane bez przypięć pokazywałoby „czeka” przy czynszu, który zszedł tydzień temu — dlatego oba kroki
    /// idą razem albo wcale.
    /// </remarks>
    public async Task<StandingOrderSavedResponseDto> HandleAsync(SaveStandingOrderRequestDto request, CancellationToken ct)
    {
        var valid = StandingOrderRuleValidator.Validate(request);
        var budget = await scope.SingleAsync(valid.BudgetId, ct);

        var order = new StandingOrder(
            budget, valid.Name, valid.ExpectedAmount, valid.Rhythm, valid.DueMonth, scope.Now(),
            currentUser.UserId);
        order.ReplaceRules(valid.Rules);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.StandingOrders.Add(order);
        await db.SaveChangesAsync(ct);
        var linked = await matcher.RematchAsync(order, ct);
        await transaction.CommitAsync(ct);

        return new StandingOrderSavedResponseDto(order.BusinessId, linked);
    }
}
