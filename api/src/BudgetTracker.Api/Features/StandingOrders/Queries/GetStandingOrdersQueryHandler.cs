using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.StandingOrders.Consts;
using BudgetTracker.Api.Features.StandingOrders.Contracts;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Queries;

/// <summary>Zlecenia stałe budżetu ze stanem w oglądanym miesiącu i ostatnio przypiętymi transakcjami.</summary>
public sealed class GetStandingOrdersQueryHandler(AppDbContext db, StandingOrdersBudgetScope scope)
{
    /// <summary>Ile ostatnich przypięć pokazuje karta po prawej.</summary>
    private const int RecentCount = 5;

    /// <summary>Tolerancja porównania kwot — grosz. Mniejsza różnica to zaokrąglenie, nie „inna kwota”.</summary>
    private const decimal AmountTolerance = 0.01m;

    /// <summary>Ekran zleceń stałych dla budżetu (wskazanego albo domyślnego) i miesiąca (wskazanego albo bieżącego).</summary>
    public async Task<StandingOrdersResponseDto> HandleAsync(Guid? budgetId, DateOnly? month, CancellationToken ct)
    {
        var currentMonth = scope.CurrentMonth();
        var viewed = month is { } m ? new DateOnly(m.Year, m.Month, 1) : currentMonth;

        var budgets = await scope.OptionsAsync(ct);
        if (StandingOrdersBudgetScope.Resolve(budgets, budgetId, currentMonth) is not { } budget)
        {
            return new StandingOrdersResponseDto(viewed, currentMonth, [], [], 0m, 0, 0, 0m, 0, [], budgets);
        }

        var orders = await db.StandingOrders
            .Where(o => o.BudgetBusinessId == budget)
            .OrderBy(o => o.Name).ThenBy(o => o.Id)
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.BusinessId).ToList();
        var pins = await db.Transactions
            .Where(t => t.StandingOrderBusinessId != null && orderIds.Contains(t.StandingOrderBusinessId.Value))
            .Select(t => new Pin(t.BusinessId, t.StandingOrderBusinessId!.Value, t.Date, -t.Amount, t.CategoryId))
            .ToListAsync(ct);

        var categoryIds = pins.Where(p => p.CategoryId != null).Select(p => p.CategoryId!.Value).Distinct().ToList();
        var categoryNames = await db.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var pinsByOrder = pins.ToLookup(p => p.OrderId);
        var rows = orders
            .Select(o => Row(o, pinsByOrder[o.BusinessId].ToList(), viewed, currentMonth, categoryNames))
            .ToList();

        var byId = orders.ToDictionary(o => o.BusinessId);
        var recent = pins
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.TransactionId)
            .Take(RecentCount)
            .Select(p => new StandingOrderPinResponseDto(
                p.TransactionId, p.OrderId, byId[p.OrderId].Name, p.Date, p.Amount,
                Math.Abs(p.Amount - byId[p.OrderId].ExpectedAmount) >= AmountTolerance))
            .ToList();

        var due = rows.Where(r => r.State is not (StandingOrderMonthState.NotDue or StandingOrderMonthState.Ended)).ToList();

        return new StandingOrdersResponseDto(
            Month: viewed,
            CurrentMonth: currentMonth,
            Orders: rows,
            Recent: recent,
            MonthlyTotal: Math.Round(orders.Where(o => !o.IsEndedBefore(viewed)).Sum(MonthlyEquivalent), 2),
            DueCount: due.Count,
            PaidCount: due.Count(r => r.State is StandingOrderMonthState.Paid or StandingOrderMonthState.PaidDifferentAmount),
            WaitingAmount: rows.Where(r => r.State == StandingOrderMonthState.Waiting).Sum(r => r.ExpectedAmount),
            DifferentAmountCount: rows.Count(r => r.State == StandingOrderMonthState.PaidDifferentAmount),
            SelectedBudgetIds: [budget],
            Budgets: budgets);
    }

    /// <summary>Wiersz tabeli ze stanem w miesiącu.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Zlecenie, które zeszło, jest „zeszło” także poza swoim miesiącem (roczne zapłacone wcześniej) — przypięta
    /// transakcja jest faktem mocniejszym niż kalendarz zlecenia.</item>
    /// <item>„Czeka” tylko w miesiącu, który TRWA. W zamkniętym ta sama sytuacja to „nie zeszło” — inaczej ekran
    /// sprzed pół roku wiecznie „czekałby” na zapłatę.</item>
    /// <item>Po ostatnim miesiącu zlecenie jest „zakończone” bez względu na przypięcia — to decyzja użytkownika,
    /// silniejsza niż transakcja przypięta, zanim zlecenie zakończył.</item>
    /// </list>
    /// </remarks>
    private static StandingOrderRowResponseDto Row(
        StandingOrder order, List<Pin> pins, DateOnly month, DateOnly currentMonth, IReadOnlyDictionary<int, string> categoryNames)
    {
        var end = month.AddMonths(1);
        var ended = order.IsEndedBefore(month);
        var inMonth = ended ? [] : pins.Where(p => p.Date >= month && p.Date < end).ToList();
        var paid = inMonth.Sum(p => p.Amount);

        var state = ended ? StandingOrderMonthState.Ended
            : inMonth.Count > 0
            ? Math.Abs(paid - order.ExpectedAmount) >= AmountTolerance
                ? StandingOrderMonthState.PaidDifferentAmount
                : StandingOrderMonthState.Paid
            : !order.IsDueIn(month) ? StandingOrderMonthState.NotDue
            : month < currentMonth ? StandingOrderMonthState.Missed
            : month == currentMonth ? StandingOrderMonthState.Waiting
            : StandingOrderMonthState.NotDue;

        var category = pins
            .Where(p => p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => categoryNames.GetValueOrDefault(g.Key))
            .FirstOrDefault();

        return new StandingOrderRowResponseDto(
            Id: order.BusinessId,
            Name: order.Name,
            ExpectedAmount: order.ExpectedAmount,
            Rhythm: order.Rhythm,
            DueMonth: order.DueMonth,
            Rules: [.. order.Rules.Select(r => new StandingOrderRuleResponseDto(r.TitlePattern, r.AmountFrom, r.AmountTo))],
            EndMonth: order.EndMonth,
            CategoryName: category,
            State: state,
            PaidOn: inMonth.Count > 0 ? inMonth.Max(p => p.Date) : null,
            PaidAmount: inMonth.Count > 0 ? paid : null,
            UsualDay: UsualDay(pins),
            LinkedCount: pins.Count);
    }

    /// <summary>Mediana dnia miesiąca z historii przypięć — „zwykle do 15.”.</summary>
    /// <remarks>Mediana, nie średnia: jedno spóźnione zlecenie (np. przez święta) nie ma przesuwać „zwykle”.</remarks>
    private static int? UsualDay(List<Pin> pins)
    {
        if (pins.Count == 0) return null;
        var days = pins.Select(p => p.Date.Day).Order().ToList();
        return days[(days.Count - 1) / 2];
    }

    /// <summary>Zlecenie w przeliczeniu na miesiąc.</summary>
    private static decimal MonthlyEquivalent(StandingOrder order) => order.Rhythm switch
    {
        StandingOrderRhythm.Quarterly => order.ExpectedAmount / 3m,
        StandingOrderRhythm.Yearly => order.ExpectedAmount / 12m,
        _ => order.ExpectedAmount,
    };

    /// <summary>Przypięta transakcja — kwota już dodatnia.</summary>
    private sealed record Pin(Guid TransactionId, Guid OrderId, DateOnly Date, decimal Amount, int? CategoryId);
}
