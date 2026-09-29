using BudgetTracker.Api.Domain;

namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>Okres rozliczeniowy jednego budżetu widziany „dziś": dzień początku i klucz bieżącego okresu.</summary>
/// <param name="StartDay">Dzień początku okresu (<see cref="Budget.PeriodStartDay"/>).</param>
/// <param name="CurrentKey">Klucz bieżącego okresu — pierwszy dzień miesiąca, w którym okres się kończy.</param>
public sealed record PeriodContext(int StartDay, DateOnly CurrentKey)
{
    /// <summary>Okres bieżący dla budżetu z dniem początku <paramref name="startDay"/> i dnia <paramref name="today"/>.</summary>
    public static PeriodContext At(int startDay, DateOnly today) => new(startDay, BillingPeriod.KeyOf(today, startDay));

    /// <summary>Pierwszy dzień okresu o kluczu <paramref name="key"/> (włącznie).</summary>
    public DateOnly From(DateOnly key) => BillingPeriod.From(key, StartDay);

    /// <summary>Pierwszy dzień PO okresie o kluczu <paramref name="key"/> — górna granica zakresu dat.</summary>
    public DateOnly ToExclusive(DateOnly key) => BillingPeriod.ToExclusive(key, StartDay);

    /// <summary>Ostatni dzień okresu o kluczu <paramref name="key"/> (włącznie).</summary>
    public DateOnly To(DateOnly key) => BillingPeriod.To(key, StartDay);
}
