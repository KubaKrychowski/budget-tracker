using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Commands;

/// <summary>„Zakończ” i „Wznów” zlecenie stałe.</summary>
/// <remarks>
/// ⚠️ Żadne z nich NIE przelicza przypięć: dialog obiecuje, że przypięte transakcje zostają. Zakończenie działa od
/// następnego importu (i przy następnej zmianie reguł) — po ostatnim miesiącu nic nowego się nie przypnie.
/// </remarks>
public sealed class EndStandingOrderCommandHandler(AppDbContext db)
{
    public async Task EndAsync(Guid id, EndStandingOrderRequestDto request, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        order.End(request.LastMonth);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResumeAsync(Guid id, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        order.Resume();
        await db.SaveChangesAsync(ct);
    }

    private async Task<Domain.StandingOrder> FindAsync(Guid id, CancellationToken ct) =>
        await db.StandingOrders.FirstOrDefaultAsync(o => o.BusinessId == id, ct)
        ?? throw new StandingOrderNotFoundException(id);
}
