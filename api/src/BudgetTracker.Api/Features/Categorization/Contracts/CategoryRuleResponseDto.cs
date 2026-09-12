using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>
/// Reguła w postaci, w jakiej wychodzi na zewnątrz.
/// </summary>
/// <remarks>
/// Kategoria jest podana <b>nazwą i publicznym identyfikatorem</b>, nie kluczem zapisu —
/// <c>CategoryId</c> jest wewnętrzny (patrz <see cref="Entity"/>). Nazwa jedzie razem z nim,
/// bo lista reguł bez niej byłaby listą Guidów.
/// </remarks>
public sealed record CategoryRuleResponseDto(
    Guid Id,
    string? Pattern,
    string? TransactionTypePattern,
    RuleDirection Direction,
    Guid CategoryId,
    string CategoryName,
    int Priority,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? Note);
