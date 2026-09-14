namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>
/// Zmiana powiązania budżetu z budżetem oszczędnościowym i jego reguł transferu.
/// </summary>
/// <param name="LinkedSavingsBudgetId">
/// <c>BusinessId</c> budżetu oszczędnościowego; <c>null</c> zdejmuje powiązanie (reguły idą wtedy w komplet).
/// </param>
/// <param name="Rules">
/// Reguły dopasowania. Puste są dozwolone TYLKO gdy <see cref="LinkedSavingsBudgetId"/> jest <c>null</c> —
/// powiązanie bez ani jednej reguły nigdy niczego by nie wykluczyło z sum.
/// </param>
public sealed record UpdateSavingsLinkRequestDto(
    Guid? LinkedSavingsBudgetId, IReadOnlyList<TitleAmountRuleRequestDto> Rules);
