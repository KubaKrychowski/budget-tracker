using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Queries;

/// <summary>Jedna strategia z policzonym wynikiem — wszystko, czego potrzebuje ekran tablicy.</summary>
public sealed class GetStrategyQueryHandler(AppDbContext db)
{
    public async Task<StrategyResponseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.BusinessId == id, ct)
            ?? throw new StrategyNotFoundException(id);

        return StrategyMapping.ToResponse(strategy, StrategySimulator.Run(StrategyMapping.ToInput(strategy)));
    }
}
