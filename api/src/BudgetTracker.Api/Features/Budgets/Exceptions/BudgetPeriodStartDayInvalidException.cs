namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>Dzień początku okresu rozliczeniowego poza zakresem 1–28 — dni 29–31 nie istnieją w każdym miesiącu.</summary>
public sealed class BudgetPeriodStartDayInvalidException : Exception;
