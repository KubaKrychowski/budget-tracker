namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Wyniki symulacji bazowej i wszystkich wariantów niezapisanego grafu — jednym żądaniem.</summary>
/// <param name="Base">Wynik wariantu bazowego (bez wyłączeń).</param>
/// <param name="Variants">Wyniki wariantów w kolejności z żądania.</param>
public sealed record StrategyVariantsResultResponseDto(
    StrategyResultResponseDto Base,
    IReadOnlyList<StrategyVariantResultResponseDto> Variants);
