namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>
/// Cała strategia do zapisu albo do podglądu symulacji (<c>POST /api/strategies/simulate</c> niczego nie zapisuje).
/// </summary>
/// <param name="Name">Nazwa strategii, 1–100 znaków.</param>
/// <param name="StartMonth">Pierwszy miesiąc symulacji — dowolny dzień miesiąca, serwer sprowadza go do pierwszego.</param>
/// <param name="StartCash">Gotówka (oszczędności) na początku pierwszego miesiąca.</param>
/// <param name="HorizonMonths">Ile miesięcy liczyć, 6–60.</param>
/// <param name="Nodes">Kafelki, do 200.</param>
/// <param name="Edges">Połączenia, do 400.</param>
public sealed record SaveStrategyRequestDto(
    string Name,
    DateOnly StartMonth,
    decimal StartCash,
    int HorizonMonths,
    IReadOnlyList<StrategyNodeRequestDto> Nodes,
    IReadOnlyList<StrategyEdgeRequestDto> Edges);
