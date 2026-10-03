using BudgetTracker.Api.Domain;

namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Graf startowy z szablonu: gotówka początkowa, kafelki i połączenia.</summary>
public sealed record StrategyTemplateGraph(decimal StartCash, IReadOnlyList<StrategyNode> Nodes, IReadOnlyList<StrategyEdge> Edges);
