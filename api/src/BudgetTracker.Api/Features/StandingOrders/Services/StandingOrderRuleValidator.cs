using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Exceptions;

namespace BudgetTracker.Api.Features.StandingOrders.Services;

/// <summary>Walidacja zlecenia przed zapisem — wspólna dla utworzenia i zmiany.</summary>
public static class StandingOrderRuleValidator
{
    /// <summary>Najkrótsza fraza reguły. Krótsza pasuje do zbyt wielu opisów, żeby coś znaczyć.</summary>
    public const int MinPatternLength = 3;

    /// <summary>Sprawdza żądanie i zwraca je z przyciętymi tekstami i miesiącem wyzerowanym przy rytmie miesięcznym.</summary>
    public static SaveStandingOrderRequestDto Validate(SaveStandingOrderRequestDto request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0) throw new StandingOrderNameRequiredException();

        var pattern = ValidatePattern(request.TitlePattern);
        ValidateRange(request.AmountFrom, request.AmountTo);
        if (request.ExpectedAmount <= 0) throw new StandingOrderAmountInvalidException();

        int? dueMonth = request.Rhythm == StandingOrderRhythm.Monthly ? null : request.DueMonth;
        if (request.Rhythm != StandingOrderRhythm.Monthly && dueMonth is not (>= 1 and <= 12))
        {
            throw new StandingOrderDueMonthInvalidException();
        }

        return request with { Name = name, TitlePattern = pattern, DueMonth = dueMonth };
    }

    /// <summary>Przycięta fraza reguły; za krótka to 400.</summary>
    public static string ValidatePattern(string? pattern)
    {
        var trimmed = pattern?.Trim() ?? "";
        return trimmed.Length < MinPatternLength ? throw new StandingOrderPatternInvalidException() : trimmed;
    }

    /// <summary>Zakres kwot: nieujemny i nieodwrócony.</summary>
    public static void ValidateRange(decimal from, decimal to)
    {
        if (from < 0 || to <= 0 || from > to) throw new StandingOrderAmountInvalidException();
    }
}
