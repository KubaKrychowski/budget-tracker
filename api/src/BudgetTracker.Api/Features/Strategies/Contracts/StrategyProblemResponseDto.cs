using BudgetTracker.Api.Features.Strategies.Consts;

namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Problem na węźle — klient mapuje rodzaj na komunikat i rysuje czerwony znacznik.</summary>
public sealed record StrategyProblemResponseDto(string NodeId, StrategyProblemKind Kind);
