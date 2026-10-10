namespace BudgetTracker.Api.Features.Strategies.Exceptions;

/// <summary>
/// Wariant jest niepoprawny: brak nazwy albo nazwa za długa, powtórzony identyfikator lub nazwa, zbyt wiele wariantów
/// albo wyłączony kafelek, którego nie ma w grafie.
/// </summary>
/// <remarks>Identyfikator kafelka w CIELE żądania, którego nie ma, to 400, nie 404 (api/CLAUDE.md).</remarks>
public sealed class StrategyVariantInvalidException : Exception;
