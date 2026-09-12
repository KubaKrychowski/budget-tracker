using BudgetTracker.Api.Features.Dashboard.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Czyste testy reguły domyślnego okresu — bez bazy i bez hosta HTTP.
/// </summary>
public class DashboardPeriodTests
{
    [Fact]
    public void Default_period_ends_today()
    {
        var (_, to) = DashboardPeriod.Default(new DateOnly(2026, 9, 1));

        Assert.Equal(new DateOnly(2026, 9, 1), to);
    }

    [Fact]
    public void Default_period_spans_thirty_inclusive_days()
    {
        var (from, to) = DashboardPeriod.Default(new DateOnly(2026, 9, 1));

        // Zakres jest domknięty obustronnie, więc 30 dni to różnica 29.
        Assert.Equal(29, to.DayNumber - from.DayNumber);
        Assert.Equal(new DateOnly(2026, 8, 3), from);
    }

    [Fact]
    public void Default_period_reaches_back_into_the_previous_month_on_the_first()
    {
        // To jest cały powód zmiany: reguła „od 1. dnia miesiąca" dawała tu jeden dzień
        // i dashboard pokazywał same zera mimo pełnej historii wydatków.
        var (from, to) = DashboardPeriod.Default(new DateOnly(2026, 9, 1));

        Assert.True(from < new DateOnly(2026, 9, 1));
        Assert.NotEqual(from, to);
    }

    [Fact]
    public void Default_period_crosses_year_boundary_correctly()
    {
        var (from, to) = DashboardPeriod.Default(new DateOnly(2026, 1, 5));

        Assert.Equal(new DateOnly(2025, 12, 7), from);
        Assert.Equal(new DateOnly(2026, 1, 5), to);
    }
}
