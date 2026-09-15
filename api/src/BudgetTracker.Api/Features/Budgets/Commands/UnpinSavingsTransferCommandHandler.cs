using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>„Odepnij” — ręczne zdjęcie transakcji z transferu do budżetu oszczędnościowego, przypiętej przez pomyłkę.</summary>
public sealed class UnpinSavingsTransferCommandHandler(AppDbContext db)
{
    /// <summary>Odpina transakcję i zapamiętuje, od którego budżetu — ponowne dopasowanie jej nie przypnie.</summary>
    /// <remarks>Nieznana albo nieprzypięta transakcja to 404: nie ma czego odpinać, a ekran jest nieaktualny.</remarks>
    public async Task HandleAsync(Guid transactionId, CancellationToken ct)
    {
        var transaction = await db.Transactions
            .FirstOrDefaultAsync(t => t.BusinessId == transactionId && t.SavingsTransferBudgetBusinessId != null, ct)
            ?? throw new SavingsTransferPinNotFoundException(transactionId);

        transaction.UnpinFromSavingsTransfer();
        await db.SaveChangesAsync(ct);
    }
}
