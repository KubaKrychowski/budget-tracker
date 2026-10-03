namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Strategia z policzonym wynikiem — jednym żądaniem wszystko, co potrzebuje ekran tablicy.</summary>
/// <param name="Id">Identyfikator strategii (<c>BusinessId</c>).</param>
/// <param name="BudgetId">Budżet strategii.</param>
/// <param name="Name">Nazwa.</param>
/// <param name="StartMonth">Pierwszy miesiąc symulacji.</param>
/// <param name="StartCash">Gotówka na początku pierwszego miesiąca.</param>
/// <param name="HorizonMonths">Ile miesięcy liczy symulacja.</param>
/// <param name="UpdatedAt">Ostatni zapis.</param>
/// <param name="Nodes">Kafelki.</param>
/// <param name="Edges">Połączenia.</param>
/// <param name="Result">Wynik symulacji dla zapisanego grafu.</param>
public sealed record StrategyResponseDto(
    Guid Id,
    Guid BudgetId,
    string Name,
    DateOnly StartMonth,
    decimal StartCash,
    int HorizonMonths,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<StrategyNodeResponseDto> Nodes,
    IReadOnlyList<StrategyEdgeResponseDto> Edges,
    StrategyResultResponseDto Result);
