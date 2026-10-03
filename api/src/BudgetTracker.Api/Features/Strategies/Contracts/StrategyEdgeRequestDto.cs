using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Połączenie w żądaniu zapisu strategii — pola jak w <c>StrategyEdge</c>.</summary>
public sealed record StrategyEdgeRequestDto(string Id, string From, string To, StrategyEdgeLabel Label);
