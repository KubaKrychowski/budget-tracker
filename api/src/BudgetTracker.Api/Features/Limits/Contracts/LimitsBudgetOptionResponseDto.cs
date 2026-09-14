namespace BudgetTracker.Api.Features.Limits.Contracts;

/// <summary>Budżet jako pozycja przełącznika nad ekranem limitów.</summary>
/// <remarks>
/// ⚠️ Świadomy bliźniak opcji z Transactions i Savings — slice ma własny kontrakt (CLAUDE.md §4).
/// Wyłączonych budżetów nie ukrywamy (CLAUDE.md §5).
/// </remarks>
public sealed record LimitsBudgetOptionResponseDto(Guid Id, string Name, DateOnly Month, bool Disabled);
