using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.EpisodicOrders.Commands;

/// <summary>Zmiana zlecenia epizodycznego — nazwa i opis zawsze, plan tylko póki zlecenie jest zaplanowane.</summary>
/// <remarks>
/// ⚠️ Nierozliczona rezerwacja zlecenia dostaje TE SAME nazwę, kwotę i termin. Kwotą i terminem rządzi zlecenie
/// (decyzja użytkownika) — bez tego zmiana planu z 4 000 na 3 500 zł zostawiłaby kopertę zbierającą 4 000.
/// Rozliczonej nie ruszamy: jej kwota to już historia wypłaty.
/// </remarks>
public sealed class UpdateEpisodicOrderCommandHandler(
    AppDbContext db, EpisodicOrderLookup orders, EpisodicOrderRequestValidator validator)
{
    public async Task HandleAsync(Guid id, SaveEpisodicOrderRequestDto request, CancellationToken ct)
    {
        var order = await orders.FindAsync(id, ct);
        var (name, description) = EpisodicOrderRequestValidator.Texts(request);
        order.Rename(name, description);

        if (!order.IsRealized)
        {
            var (categoryId, amount, dueMonth) = await validator.PlanAsync(request, ct);
            order.Plan(categoryId, amount, dueMonth);
        }

        var reservation = await orders.ReservationOfAsync(order, ct);
        if (reservation is { SettledAt: null } && order is { PlannedAmount: { } planned })
        {
            reservation.Update(order.Name, planned, order.DueMonth, reservation.Priority);
        }

        await db.SaveChangesAsync(ct);
    }
}
