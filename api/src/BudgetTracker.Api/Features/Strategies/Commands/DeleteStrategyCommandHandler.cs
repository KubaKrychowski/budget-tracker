using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Commands;

/// <summary>Usuwa strategię (stempel <c>DeletedAt</c>). Nic, co z niej zastosowano w budżecie, nie znika.</summary>
public sealed class DeleteStrategyCommandHandler(AppDbContext db)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.BusinessId == id, ct)
            ?? throw new StrategyNotFoundException(id);

        db.Remove(strategy);
        await db.SaveChangesAsync(ct);
    }
}
