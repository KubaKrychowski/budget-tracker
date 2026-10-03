using BudgetTracker.Api.Domain;

namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Wejście symulatora — wszystko, czego potrzebuje, żeby policzyć strategię bez bazy i zegara.</summary>
public sealed record StrategyInput(
    DateOnly StartMonth,
    decimal StartCash,
    int HorizonMonths,
    IReadOnlyList<StrategyNode> Nodes,
    IReadOnlyList<StrategyEdge> Edges);
