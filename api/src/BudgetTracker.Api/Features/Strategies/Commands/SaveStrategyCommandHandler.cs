using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Commands;

/// <summary>Zapisuje całą strategię (parametry i graf) i zwraca ją z wynikiem symulacji.</summary>
/// <remarks>
/// Zapis jest całościowy (PUT): klient trzyma stan tablicy i wysyła go w całości. Szkic z problemami zapisuje się zawsze;
/// odrzucany jest tylko graf strukturalnie niepoprawny (<see cref="StrategyGraphValidator"/>).
/// </remarks>
public sealed class SaveStrategyCommandHandler(AppDbContext db, TimeProvider clock)
{
    public async Task<StrategyResponseDto> HandleAsync(Guid id, SaveStrategyRequestDto request, CancellationToken ct)
    {
        var valid = StrategyGraphValidator.Validate(request);
        var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.BusinessId == id, ct)
            ?? throw new StrategyNotFoundException(id);

        strategy.Replace(
            valid.Name, valid.StartMonth, valid.StartCash, valid.HorizonMonths, valid.Nodes, valid.Edges, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        return StrategyMapping.ToResponse(strategy, StrategySimulator.Run(StrategyMapping.ToInput(strategy)));
    }
}
