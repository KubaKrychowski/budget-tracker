using BudgetTracker.Api.Features.StandingOrders.Exceptions;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Commands;

/// <summary>Usuwa zlecenie stałe i zdejmuje jego przypięcia.</summary>
public sealed class DeleteStandingOrderCommandHandler(AppDbContext db, StandingOrderMatcher matcher)
{
    /// <summary>Kasowanie logiczne zlecenia, przypięcia i pamięć odpięć znikają fizycznie z transakcji.</summary>
    /// <remarks>
    /// Transakcje zostają bez zmian (poza przypięciem) — zlecenie nigdy nie ruszało ich kategorii, więc nie ma czego
    /// przywracać.
    /// </remarks>
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var order = await db.StandingOrders.FirstOrDefaultAsync(o => o.BusinessId == id, ct)
            ?? throw new StandingOrderNotFoundException(id);

        await matcher.ForgetAsync(order.BusinessId, ct);
        db.StandingOrders.Remove(order);
        await db.SaveChangesAsync(ct);
    }
}
