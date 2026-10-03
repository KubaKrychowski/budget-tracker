using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Services;

namespace BudgetTracker.Api.Features.Strategies.Queries;

/// <summary>Podgląd symulacji niezapisanego grafu — ekran liczy wynik na bieżąco, przy każdej zmianie tablicy.</summary>
/// <remarks>
/// Niczego nie zapisuje i nie czyta z bazy, ale przechodzi tę samą walidację strukturalną co zapis, więc graf, którego
/// nie da się zapisać, nie da się też „podejrzeć”.
/// </remarks>
public sealed class SimulateStrategyQueryHandler
{
    public Task<StrategyResultResponseDto> HandleAsync(SaveStrategyRequestDto request, CancellationToken ct)
    {
        var valid = StrategyGraphValidator.Validate(request);
        return Task.FromResult(StrategyMapping.ToResult(StrategySimulator.Run(StrategyMapping.ToInput(valid))));
    }
}
