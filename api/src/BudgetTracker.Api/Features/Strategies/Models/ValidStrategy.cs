using BudgetTracker.Api.Domain;

namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Żądanie zapisu po walidacji i normalizacji (nazwa przycięta, miesiące sprowadzone do pierwszego dnia).</summary>
public sealed record ValidStrategy(
    string Name,
    DateOnly StartMonth,
    decimal StartCash,
    int HorizonMonths,
    IReadOnlyList<StrategyNode> Nodes,
    IReadOnlyList<StrategyEdge> Edges);
