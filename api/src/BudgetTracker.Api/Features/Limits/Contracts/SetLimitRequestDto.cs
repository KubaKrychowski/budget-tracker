namespace BudgetTracker.Api.Features.Limits.Contracts;

/// <summary>Ustawienie albo zmiana limitu kategorii.</summary>
/// <param name="BudgetId">Budżet; <c>null</c> = budżet domyślny, ta sama reguła co przy odczycie.</param>
/// <param name="ValidFrom">Miesiąc, od którego limit obowiązuje — dzień jest ignorowany.</param>
/// <param name="WarningThreshold">Od ilu procent limitu ostrzegać (1–100).</param>
public sealed record SetLimitRequestDto(
    Guid? BudgetId,
    Guid CategoryId,
    decimal Amount,
    int WarningThreshold,
    DateOnly ValidFrom);
