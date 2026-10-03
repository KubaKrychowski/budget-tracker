using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Połączenie strategii w odpowiedzi — pola jak w <c>StrategyEdge</c>.</summary>
public sealed record StrategyEdgeResponseDto(string Id, string From, string To, StrategyEdgeLabel Label);
