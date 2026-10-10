using BudgetTracker.Api.Domain;

namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Wejście symulatora — wszystko, czego potrzebuje, żeby policzyć strategię bez bazy i zegara.</summary>
/// <param name="StartMonth">Pierwszy miesiąc symulacji.</param>
/// <param name="StartCash">Gotówka na początku pierwszego miesiąca.</param>
/// <param name="HorizonMonths">Ile miesięcy liczyć.</param>
/// <param name="Nodes">Kafelki.</param>
/// <param name="Edges">Połączenia.</param>
/// <param name="DisabledNodeIds">Kafelki wyłączone w liczonym wariancie; pusty zbiór = wariant bazowy.</param>
public sealed record StrategyInput(
    DateOnly StartMonth,
    decimal StartCash,
    int HorizonMonths,
    IReadOnlyList<StrategyNode> Nodes,
    IReadOnlyList<StrategyEdge> Edges,
    IReadOnlySet<string>? DisabledNodeIds = null);
