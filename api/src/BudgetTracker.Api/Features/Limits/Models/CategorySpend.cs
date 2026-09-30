namespace BudgetTracker.Api.Features.Limits.Models;

/// <summary>Wydane w kategorii w okresie, jako wartość dodatnia, i liczba transakcji, które to składają.</summary>
public sealed record CategorySpend(decimal Spent, int Count);
