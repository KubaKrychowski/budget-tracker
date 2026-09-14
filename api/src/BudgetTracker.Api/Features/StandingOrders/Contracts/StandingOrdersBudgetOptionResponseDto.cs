namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Budżet jako pozycja przełącznika nad ekranem zleceń stałych.</summary>
/// <remarks>⚠️ Świadomy bliźniak opcji z innych slice'ów — slice ma własny kontrakt (CLAUDE.md §4).</remarks>
public sealed record StandingOrdersBudgetOptionResponseDto(Guid Id, string Name, DateOnly Month, bool Disabled);
