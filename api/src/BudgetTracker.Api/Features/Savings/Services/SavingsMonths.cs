namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>Arytmetyka miesięcy kalendarzowych — werdykty, cele i terminy rezerwacji dotyczą całych miesięcy.</summary>
public static class SavingsMonths
{
    /// <summary>Pierwszy dzień miesiąca daty — tak normalizowane są cele i terminy.</summary>
    public static DateOnly FirstDayOf(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>Jak <see cref="FirstDayOf(DateOnly)"/>, z zachowaniem braku terminu.</summary>
    public static DateOnly? FirstDayOf(DateOnly? date) => date is { } d ? FirstDayOf(d) : null;

    /// <summary>Liczba miesięcy od <paramref name="b"/> do <paramref name="a"/> (ujemna, gdy <paramref name="a"/> jest wcześniej).</summary>
    public static int MonthsBetween(DateOnly a, DateOnly b) =>
        ((a.Year - b.Year) * 12) + (a.Month - b.Month);
}
