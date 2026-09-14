using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Exceptions;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Commands;

/// <summary>Zmienia zlecenie stałe i przelicza jego przypięcia według nowej reguły.</summary>
public sealed class UpdateStandingOrderCommandHandler(AppDbContext db, StandingOrderMatcher matcher)
{
    /// <summary>Zmiana i przeliczenie przypięć w jednej transakcji bazodanowej.</summary>
    /// <remarks>
    /// Budżet z żądania jest ignorowany — zlecenie nie przechodzi między budżetami. Ręczne odpięcia od tego zlecenia
    /// przeżywają zmianę reguły (patrz <see cref="StandingOrderMatcher"/>).
    /// </remarks>
    public async Task<StandingOrderSavedResponseDto> HandleAsync(Guid id, SaveStandingOrderRequestDto request, CancellationToken ct)
    {
        var valid = StandingOrderRuleValidator.Validate(request);
        var order = await db.StandingOrders.FirstOrDefaultAsync(o => o.BusinessId == id, ct)
            ?? throw new StandingOrderNotFoundException(id);

        order.Change(valid.Name, valid.ExpectedAmount, valid.Rhythm, valid.DueMonth,
            valid.TitlePattern, valid.AmountFrom, valid.AmountTo);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        var linked = await matcher.RematchAsync(order, ct);
        await transaction.CommitAsync(ct);

        return new StandingOrderSavedResponseDto(order.BusinessId, linked);
    }
}
