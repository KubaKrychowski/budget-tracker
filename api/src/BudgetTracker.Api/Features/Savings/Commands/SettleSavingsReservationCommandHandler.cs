using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>
/// Rozliczenie rezerwacji — wskazuje realną wypłatę z oszczędności.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Nigdy automatycznie.</b> Pomyłka tutaj zamyka kopertę, która wcale nie została
/// opłacona, a tego nikt nie zauważy — dlatego serwer tylko sprawdza kandydata,
/// a wybiera człowiek (tekst jest w makiecie 170:1727).
/// </para>
/// <para>
/// Zamyka rezerwację: przestaje przyjmować wpłaty i pomniejszać wolne środki — patrz doc przy
/// <see cref="SavingsReservationsResponseDto.FreeFunds"/>.
/// </para>
/// </remarks>
public sealed class SettleSavingsReservationCommandHandler(
    AppDbContext db,
    ReservationLookup reservations,
    SavingsCategory savingsCategory,
    SavingsBudgetScope scope,
    TimeProvider clock)
{
    /// <summary>Zamyka rezerwację <paramref name="id"/> wskazaną wypłatą.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Cztery warunki bronią jednej rzeczy: żeby „rozliczone" znaczyło „naprawdę zapłacone", a nie „ktoś kliknął
    /// dowolny wiersz z wyciągu" — ten sam budżet, kategoria „Oszczędności", kwota DODATNIA (wypłata Z oszczędności,
    /// konwencja znaku z <c>Transaction.Amount</c>) i żadna inna rezerwacja nie jest nią już rozliczona.</item>
    /// <item>Nieznana transakcja idzie tą samą drogą co niepasująca — 400, nie 404. Identyfikator przychodzi w CIELE
    /// żądania, więc 404 mówiłby „nie ma takiego endpointu", a nie „ten wiersz nie nadaje się na rozliczenie".
    /// Ta sama zasada co przy imporcie.</item>
    /// <item><c>transaction!</c> po sprawdzeniu, bo kompilator nie przenosi zawężenia z wyrażenia <c>usable</c>
    /// przez <c>if</c>.</item>
    /// </list>
    /// </remarks>
    public async Task<SavingsReservationResponseDto> HandleAsync(
        Guid id, SettleReservationRequestDto request, CancellationToken ct)
    {
        var reservation = await reservations.FindAsync(id, ct);
        if (reservation.SettledAt is not null) throw new ReservationAlreadySettledException();

        var savingsCategoryId = await savingsCategory.IdAsync(ct);

        var transaction = await db.Transactions
            .Where(t => t.BusinessId == request.TransactionId)
            .FirstOrDefaultAsync(ct);

        var usable =
            transaction is not null
            && transaction.BudgetBusinessId == reservation.BudgetBusinessId
            && savingsCategoryId is not null && transaction.CategoryId == savingsCategoryId
            && transaction.Amount > 0
            && !await db.SavingsReservations.AnyAsync(
                other => other.SettledTransactionBusinessId == transaction.BusinessId
                         && other.Id != reservation.Id, ct);

        if (!usable) throw new SettlementTransactionInvalidException();

        reservation.Settle(clock.GetUtcNow(), transaction!.BusinessId);
        await db.SaveChangesAsync(ct);

        return ReservationViews.Single(reservation, SavingsMonths.FirstDayOf(scope.Today()));
    }
}
