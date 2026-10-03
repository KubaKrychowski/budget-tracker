using BudgetTracker.Api.Features.Strategies.Consts;

namespace BudgetTracker.Api.Features.Strategies.Models;

/// <summary>Jeden problem grafu na jednym węźle.</summary>
public sealed record StrategyProblem(string NodeId, StrategyProblemKind Kind);
