using BudgetTracker.Api.Features.StandingOrders.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.StandingOrders.Commands;

/// <summary>„Odepnij” — ręczne zdjęcie transakcji ze zlecenia stałego, przypiętej przez pomyłkę.</summary>
public sealed class UnpinTransactionCommandHandler(AppDbContext db)
{
    /// <summary>Odpina transakcję i zapamiętuje, od którego zlecenia — ponowne dopasowanie jej nie przypnie.</summary>
    /// <remarks>Nieznana albo nieprzypięta transakcja to 404: nie ma czego odpinać, a ekran jest nieaktualny.</remarks>
    public async Task HandleAsync(Guid transactionId, CancellationToken ct)
    {
        var transaction = await db.Transactions
            .FirstOrDefaultAsync(t => t.BusinessId == transactionId && t.StandingOrderBusinessId != null, ct)
            ?? throw new StandingOrderPinNotFoundException(transactionId);

        transaction.UnpinFromStandingOrder();
        await db.SaveChangesAsync(ct);
    }
}
