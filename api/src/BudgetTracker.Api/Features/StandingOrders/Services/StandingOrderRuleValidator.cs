using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Exceptions;
using BudgetTracker.Api.Features.StandingOrders.Models;

namespace BudgetTracker.Api.Features.StandingOrders.Services;

/// <summary>Walidacja zlecenia przed zapisem — wspólna dla utworzenia i zmiany.</summary>
public static class StandingOrderRuleValidator
{
    /// <summary>Najkrótsza fraza reguły. Krótsza pasuje do zbyt wielu opisów, żeby coś znaczyć.</summary>
    public const int MinPatternLength = 3;

    /// <summary>Najwięcej reguł w zleceniu — każda to osobny warunek w zapytaniu przypinającym.</summary>
    public const int MaxRules = 20;

    /// <summary>Sprawdza żądanie i zwraca je z przyciętymi tekstami i miesiącem wyzerowanym przy rytmie miesięcznym.</summary>
    public static ValidStandingOrder Validate(SaveStandingOrderRequestDto request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0) throw new StandingOrderNameRequiredException();

        var rules = ValidateRules(request.Rules);
        if (request.ExpectedAmount <= 0) throw new StandingOrderAmountInvalidException();

        int? dueMonth = request.Rhythm == StandingOrderRhythm.Monthly ? null : request.DueMonth;
        if (request.Rhythm != StandingOrderRhythm.Monthly && dueMonth is not (>= 1 and <= 12))
        {
            throw new StandingOrderDueMonthInvalidException();
        }

        return new ValidStandingOrder(request.BudgetId, name, request.ExpectedAmount, request.Rhythm, dueMonth, rules);
    }

    /// <summary>Reguły z żądania: od 1 do <see cref="MaxRules"/>, każda z frazą ≥ minimum i nieodwróconym zakresem.</summary>
    public static IReadOnlyList<StandingOrderRule> ValidateRules(IReadOnlyList<StandingOrderRuleRequestDto>? rules)
    {
        if (rules is null || rules.Count is 0 or > MaxRules) throw new StandingOrderRulesInvalidException();

        return [.. rules.Select(r =>
        {
            var pattern = r.TitlePattern?.Trim() ?? "";
            if (pattern.Length < MinPatternLength) throw new StandingOrderPatternInvalidException();
            if (r.AmountFrom < 0 || r.AmountTo <= 0 || r.AmountFrom > r.AmountTo) throw new StandingOrderAmountInvalidException();
            return new StandingOrderRule(pattern, r.AmountFrom, r.AmountTo);
        })];
    }
}
