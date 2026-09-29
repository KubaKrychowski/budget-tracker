namespace BudgetTracker.Api.Domain;

/// <summary>
/// Okres rozliczeniowy budżetu: od <see cref="Budget.PeriodStartDay"/>-tego dnia miesiąca do dnia poprzedzającego
/// ten dzień w miesiącu następnym (dzień 28: 28.09–27.10).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Okres nosi nazwę miesiąca, w którym się KOŃCZY</b> (28.09–27.10 to „październik"): pensja z 28.09 finansuje
/// październik. Kluczem okresu jest więc pierwszy dzień tego miesiąca — ten sam typ i ta sama semantyka co dotychczasowy
/// „miesiąc" (<see cref="BudgetItem.ValidFrom"/>, <c>DueMonth</c>), dzięki czemu historia limitów i zleceń nie wymaga
/// przeliczania. Dzień 1 daje zwykły miesiąc kalendarzowy, czyli dotychczasowe zachowanie.
/// </para>
/// <para>
/// Dozwolone są dni 1–28: każdy istnieje w każdym miesiącu, więc granice nie wymagają reguł dla krótkich miesięcy.
/// </para>
/// </remarks>
public static class BillingPeriod
{
    public const int MinStartDay = 1;
    public const int MaxStartDay = 28;

    /// <summary>Czy dzień początku okresu mieści się w dozwolonym zakresie.</summary>
    public static bool IsValidStartDay(int startDay) => startDay is >= MinStartDay and <= MaxStartDay;

    /// <summary>Klucz okresu, do którego należy <paramref name="date"/> — pierwszy dzień miesiąca, w którym okres się kończy.</summary>
    public static DateOnly KeyOf(DateOnly date, int startDay)
    {
        var first = new DateOnly(date.Year, date.Month, 1);
        return startDay > MinStartDay && date.Day >= startDay ? first.AddMonths(1) : first;
    }

    /// <summary>Początek okresu (włącznie) o kluczu <paramref name="key"/>.</summary>
    public static DateOnly From(DateOnly key, int startDay) =>
        startDay == MinStartDay ? key : new DateOnly(key.Year, key.Month, startDay).AddMonths(-1);

    /// <summary>Koniec okresu (wyłącznie) o kluczu <paramref name="key"/> — pierwszy dzień następnego okresu.</summary>
    public static DateOnly ToExclusive(DateOnly key, int startDay) =>
        startDay == MinStartDay ? key.AddMonths(1) : new DateOnly(key.Year, key.Month, startDay);

    /// <summary>Ostatni dzień okresu (włącznie) — do pokazania użytkownikowi jako „27.10".</summary>
    public static DateOnly To(DateOnly key, int startDay) => ToExclusive(key, startDay).AddDays(-1);
}
