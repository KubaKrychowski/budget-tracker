using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Exceptions;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>Walidacja reguł transferu przed zapisem powiązania z budżetem oszczędnościowym.</summary>
public static class SavingsTransferRuleValidator
{
    /// <summary>Najkrótsza fraza reguły. Krótsza pasuje do zbyt wielu opisów, żeby coś znaczyć.</summary>
    public const int MinPatternLength = 3;

    /// <summary>Najwięcej reguł na budżet — każda to osobny warunek w zapytaniu przypinającym.</summary>
    public const int MaxRules = 20;

    /// <summary>Reguły z żądania: od 1 do <see cref="MaxRules"/>, każda z frazą ≥ minimum i nieodwróconym zakresem.</summary>
    /// <remarks>Wymóg „co najmniej jedna" obowiązuje TYLKO gdy budżet w ogóle jest powiązany — sprawdza to wywołujący.</remarks>
    public static IReadOnlyList<TitleAmountRule> Validate(IReadOnlyList<TitleAmountRuleRequestDto>? rules)
    {
        if (rules is null || rules.Count is 0 or > MaxRules) throw new SavingsTransferRulesInvalidException();

        return [.. rules.Select(r =>
        {
            var pattern = r.TitlePattern?.Trim() ?? "";
            if (pattern.Length < MinPatternLength) throw new SavingsTransferPatternInvalidException();
            if (r.AmountFrom < 0 || r.AmountTo <= 0 || r.AmountFrom > r.AmountTo)
            {
                throw new SavingsTransferAmountInvalidException();
            }
            return new TitleAmountRule(pattern, r.AmountFrom, r.AmountTo);
        })];
    }
}
