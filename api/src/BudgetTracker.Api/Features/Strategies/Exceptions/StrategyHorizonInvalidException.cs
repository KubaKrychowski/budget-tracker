namespace BudgetTracker.Api.Features.Strategies.Exceptions;

/// <summary>Horyzont symulacji poza zakresem <c>Strategy.MinHorizonMonths</c>–<c>Strategy.MaxHorizonMonths</c>.</summary>
public sealed class StrategyHorizonInvalidException : Exception;
