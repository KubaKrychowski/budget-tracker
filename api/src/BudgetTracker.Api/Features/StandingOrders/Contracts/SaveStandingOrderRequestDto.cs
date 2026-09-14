using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.StandingOrders.Contracts;

/// <summary>Utworzenie albo zmiana zlecenia stałego.</summary>
/// <param name="BudgetId">Budżet; <c>null</c> = domyślny. Przy zmianie ignorowany — zlecenie nie przechodzi między budżetami.</param>
/// <param name="DueMonth">Miesiąc (1–12) dla rytmu rocznego i kwartalnego; przy miesięcznym ignorowany.</param>
/// <param name="TitlePattern">Fraza, którą musi zawierać opis transakcji — bez rozróżniania wielkości liter.</param>
/// <param name="AmountFrom">Dolna granica kwoty wydatku, bez znaku.</param>
/// <param name="AmountTo">Górna granica kwoty wydatku, bez znaku.</param>
public sealed record SaveStandingOrderRequestDto(
    Guid? BudgetId,
    string Name,
    decimal ExpectedAmount,
    StandingOrderRhythm Rhythm,
    int? DueMonth,
    string TitlePattern,
    decimal AmountFrom,
    decimal AmountTo);
