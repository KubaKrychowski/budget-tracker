using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>
/// Reguła przychodząca — ta sama postać przy tworzeniu i przy edycji.
/// </summary>
/// <remarks>
/// ⚠️ Oba wzorce są opcjonalne <b>osobno</b>, ale nie oba naraz: reguła bez żadnego wzorca
/// pasuje do każdej transakcji po swojej stronie przepływu. Taki wiersz odsiewa po cichu
/// <c>RuleCategorizer</c> — czyli użytkownik zapisałby regułę, zobaczył ją na liście i nigdy
/// nie zrozumiał, dlaczego nic nie robi. Stąd walidacja na wejściu (<c>CategoryRuleValidator</c>).
/// </remarks>
/// <param name="CategoryId">Publiczny <c>BusinessId</c> kategorii.</param>
public sealed record CategoryRuleRequestDto(
    string? Pattern,
    string? TransactionTypePattern,
    RuleDirection Direction,
    Guid CategoryId,
    int Priority,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? Note);
