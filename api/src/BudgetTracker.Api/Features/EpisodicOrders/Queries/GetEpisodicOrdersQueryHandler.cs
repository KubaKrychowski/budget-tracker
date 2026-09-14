using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Features.Savings.Queries;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.EpisodicOrders.Queries;

/// <summary>Ekran „Zlecenia epizodyczne” — zaplanowane i zrealizowane jednego budżetu.</summary>
/// <remarks>
/// <para>
/// ⚠️ „Uzbierane” bierzemy z <see cref="GetSavingsReservationsQueryHandler"/>, a nie liczymy tu drugi raz. Przydział
/// zależy od CAŁEJ kolejki rezerwacji i stanu konta oszczędnościowego — policzony osobno rozjechałby się z ekranem
/// rezerwacji przy pierwszej zmianie reguły kolejki.
/// </para>
/// <para>
/// ⚠️ Zrealizowane zlecenie, którego transakcji już nie ma (usunięta, reset budżetu), nie jest pokazywane ani
/// liczone — nie ma skąd wziąć jego kwoty i daty. Zaplanowane wtedy wraca do zaplanowanych. Przywrócenie transakcji
/// przywraca też zlecenie, bo jego wiersz nigdy nie został zmieniony.
/// </para>
/// </remarks>
public sealed class GetEpisodicOrdersQueryHandler(
    AppDbContext db, EpisodicOrdersBudgetScope scope, GetSavingsReservationsQueryHandler reservations)
{
    public async Task<EpisodicOrdersResponseDto> HandleAsync(Guid? budgetId, CancellationToken ct)
    {
        var currentMonth = scope.CurrentMonth();
        var budgets = await scope.OptionsAsync(ct);
        if (EpisodicOrdersBudgetScope.Resolve(budgets, budgetId, currentMonth) is not { } budget)
        {
            return new EpisodicOrdersResponseDto([], [], 0m, 0m, 0m, 0m, 0, currentMonth, [], budgets);
        }

        var orders = await db.EpisodicOrders
            .Where(o => o.BudgetBusinessId == budget)
            .ToListAsync(ct);

        var transactionIds = orders.Where(o => o.TransactionBusinessId != null).Select(o => o.TransactionBusinessId!.Value).ToList();
        var transactions = await db.Transactions
            .Where(t => transactionIds.Contains(t.BusinessId))
            .Select(t => new { t.BusinessId, t.Date, t.Amount, t.Description, t.CategoryId })
            .ToDictionaryAsync(t => t.BusinessId, ct);

        var categories = await db.Categories
            .Select(c => new { c.Id, c.BusinessId, c.Name })
            .ToDictionaryAsync(c => c.Id, ct);

        var collected = (await reservations.HandleAsync([budget], ct)).Reservations
            .ToDictionary(r => r.Id);

        var planned = new List<EpisodicOrderRowResponseDto>();
        var realized = new List<EpisodicOrderRowResponseDto>();

        foreach (var o in orders)
        {
            var category = o.CategoryId is { } planCategory ? categories.GetValueOrDefault(planCategory) : null;
            var reservation = o.ReservationBusinessId is { } rid ? collected.GetValueOrDefault(rid) : null;

            if (o.TransactionBusinessId is { } tid && transactions.TryGetValue(tid, out var t))
            {
                var txCategory = t.CategoryId is { } tc ? categories.GetValueOrDefault(tc) : null;
                realized.Add(new EpisodicOrderRowResponseDto(
                    o.BusinessId, o.Name, o.Description, category?.BusinessId, txCategory?.Name,
                    -t.Amount, o.DueMonth, t.Date, t.BusinessId, t.Description, o.WasPlanned,
                    reservation?.Id, reservation?.Collected));
            }
            else if (o is { PlannedAmount: { } amount })
            {
                planned.Add(new EpisodicOrderRowResponseDto(
                    o.BusinessId, o.Name, o.Description, category?.BusinessId, category?.Name,
                    amount, o.DueMonth, null, null, null, true, reservation?.Id, reservation?.Collected));
            }
        }

        planned = [.. planned.OrderBy(r => r.DueMonth is null).ThenBy(r => r.DueMonth).ThenBy(r => r.Name)];
        realized = [.. realized.OrderByDescending(r => r.Date).ThenBy(r => r.Name)];
        var withReservation = planned.Where(r => r.ReservationId is not null).ToList();

        return new EpisodicOrdersResponseDto(
            Planned: planned,
            Realized: realized,
            PlannedTotal: planned.Sum(r => r.Amount),
            CollectedTotal: withReservation.Sum(r => r.Collected ?? 0m),
            ReservedTotal: withReservation.Sum(r => r.Amount),
            RealizedThisYear: realized.Where(r => r.Date?.Year == currentMonth.Year).Sum(r => r.Amount),
            WithoutSavingsCount: planned.Count - withReservation.Count,
            CurrentMonth: currentMonth,
            SelectedBudgetIds: [budget],
            Budgets: budgets);
    }
}
