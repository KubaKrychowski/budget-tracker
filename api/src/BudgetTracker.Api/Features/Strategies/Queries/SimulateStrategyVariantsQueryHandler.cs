using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Services;

namespace BudgetTracker.Api.Features.Strategies.Queries;

/// <summary>Podgląd symulacji niezapisanego grafu: wariant bazowy i wszystkie warianty z żądania jednym wywołaniem.</summary>
/// <remarks>
/// Niczego nie zapisuje i nie czyta z bazy. Ekran przelicza wszystkie warianty przy każdej zmianie tablicy, bo karty
/// wariantów i porównanie pokazują ich wyniki obok siebie — jedno żądanie zamiast jednego na wariant.
/// </remarks>
public sealed class SimulateStrategyVariantsQueryHandler
{
    public Task<StrategyVariantsResultResponseDto> HandleAsync(SaveStrategyRequestDto request, CancellationToken ct)
    {
        var valid = StrategyGraphValidator.Validate(request);
        return Task.FromResult(new StrategyVariantsResultResponseDto(
            StrategyMapping.ToResult(StrategySimulator.Run(StrategyMapping.ToInput(valid))),
            [.. valid.Variants.Select(v => new StrategyVariantResultResponseDto(
                v.Id, StrategyMapping.ToResult(StrategySimulator.Run(StrategyMapping.ToInput(valid, v.Id)))))]));
    }
}
