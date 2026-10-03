using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Połączenie między kafelkami strategii — strzałka od <see cref="From"/> do <see cref="To"/>.</summary>
/// <param name="Id">Identyfikator połączenia nadany przez klienta.</param>
/// <param name="From">Identyfikator węzła, z którego wychodzi strzałka.</param>
/// <param name="To">Identyfikator węzła, do którego prowadzi.</param>
/// <param name="Label">Zwykłe połączenie albo wyjście warunku (tak/nie).</param>
public sealed record StrategyEdge(string Id, string From, string To, StrategyEdgeLabel Label);
