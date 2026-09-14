using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Queries;

/// <summary>
/// Rezerwacje na koncie oszczędnościowym — nazwane koperty na nadchodzące wydatki — razem z wolnymi środkami.
/// </summary>
/// <remarks>
/// <para>
/// Domyka lukę, którą zostawił cel oszczędnościowy (#7): <b>„wolne środki" nie miały
/// definicji</b>. Bez rezerwacji kafel pokazywałby całe oszczędności jako dostępne — w tym
/// pieniądze przypisane już na ubezpieczenie.
/// </para>
/// <para>
/// ⚠️ Osobny odczyt, nie rozrost <see cref="GetSavingsQueryHandler"/>: tamten liczy DYSCYPLINĘ
/// (ile odkładasz co miesiąc), ten liczy STAN (co z odłożonego jest wolne). Dwie różne
/// wielkości liczone z tych samych transakcji — zlanie ich w jedną klasę skończyłoby się
/// tym, że któraś reguła po cichu przecieknie do drugiej.
/// </para>
/// </remarks>
public sealed class GetSavingsReservationsQueryHandler(
    AppDbContext db, SavingsBudgetScope scope, SavingsCategory savingsCategory)
{
    /// <summary>Rezerwacje wybranych budżetów w kolejce zbierania.</summary>
    /// <remarks>
    /// Wolne środki odejmują WSZYSTKIE rezerwacje, także rozliczone — patrz
    /// <see cref="SavingsReservationsResponseDto.FreeFunds"/>.
    /// </remarks>
    public async Task<SavingsReservationsResponseDto> HandleAsync(
        IReadOnlyList<Guid>? budgetIds, CancellationToken ct)
    {
        var today = scope.Today();
        var currentMonth = SavingsMonths.FirstDayOf(today);

        var budgets = await scope.OptionsAsync(ct);
        var selected = SavingsBudgetScope.Resolve(budgets, budgetIds, today);
        if (selected.Count == 0)
        {
            return new SavingsReservationsResponseDto([], 0m, 0m, 0m, 0m, 0m, null, [], budgets);
        }

        var reservations = await db.SavingsReservations
            .Where(r => selected.Contains(r.BudgetBusinessId))
            .ToListAsync(ct);

        var balance = await AccountBalanceAsync(selected, ct);
        var views = Allocate(reservations, balance, currentMonth);

        var reservedTotal = reservations.Sum(r => r.Amount);
        var collectedTotal = views.Sum(v => v.Collected);

        return new SavingsReservationsResponseDto(
            Reservations: views,
            AccountBalance: balance,
            ReservedTotal: reservedTotal,
            SettledTotal: reservations.Where(r => r.SettledAt is not null).Sum(r => r.Amount),
            CollectedTotal: collectedTotal,
            FreeFunds: balance - reservedTotal,
            CoveredBy: await CoveredByAsync(selected, reservedTotal - collectedTotal, currentMonth, ct),
            SelectedBudgetIds: selected,
            Budgets: budgets);
    }

    /// <summary>
    /// Kolejka zbierania — kto bierze pieniądze pierwszy, gdy nie starcza dla wszystkich.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wpłata na konto oszczędnościowe nie niesie informacji, na którą rezerwację poszła, więc
    /// bez reguły „uzbierane 36%" nie znaczy nic. Reguła: <b>zbiera rezerwacja z najbliższym
    /// terminem, nadwyżka schodzi do następnej</b>. Alternatywa (proporcjonalnie do kwot) jest
    /// prostsza, ale gorsza — przy niej nic nie jest gotowe na czas.
    /// </para>
    /// <para>
    /// Rozliczone nie biorą udziału w kolejce: nie ma już czego zbierać, ich pieniądze wyszły — dlatego są pokryte
    /// z definicji. Ujemny stan konta to pusta pula, a nie ujemny przydział.
    /// </para>
    /// <para>
    /// ⚠️ <b>Trzecie kryterium sortowania (<c>Id</c>) NIE jest ozdobą.</b> Bez niego dwie
    /// rezerwacje z tym samym terminem i priorytetem zamieniałyby się miejscami między
    /// odczytami, a „uzbierane" skakałoby przy każdym odświeżeniu — błąd, którego nie widać
    /// w pojedynczym teście.
    /// </para>
    /// <para>
    /// ⚠️ Konsekwencja, o której użytkownik musi wiedzieć ZANIM zapisze: dołożenie rezerwacji
    /// z bliższym terminem <b>odbiera</b> uzbierane tym dalszym. Arytmetycznie poprawne, na
    /// ekranie wygląda jak utrata postępu — dlatego modal dodawania mówi o tym wprost.
    /// </para>
    /// </remarks>
    private static List<SavingsReservationResponseDto> Allocate(
        IReadOnlyList<SavingsReservation> reservations, decimal balance, DateOnly currentMonth)
    {
        var pool = Math.Max(balance, 0m);

        var collected = new Dictionary<int, decimal>();
        foreach (var r in reservations
            .Where(r => r.SettledAt is null)
            .OrderBy(r => r.DueMonth).ThenBy(r => r.Priority).ThenBy(r => r.Id))
        {
            var share = Math.Min(r.Amount, pool);
            collected[r.Id] = share;
            pool -= share;
        }

        return [.. reservations
            .OrderBy(r => r.DueMonth).ThenBy(r => r.Priority).ThenBy(r => r.Id)
            .Select(r => new SavingsReservationResponseDto(
                r.BusinessId,
                r.Name,
                r.Amount,
                r.DueMonth,
                r.SettledAt is not null ? r.Amount : collected.GetValueOrDefault(r.Id),
                ReservationViews.StatusOf(r, currentMonth),
                r.SettledAt is { } settled ? DateOnly.FromDateTime(settled.UtcDateTime.Date) : null,
                r.SettledTransactionBusinessId))];
    }

    /// <summary>
    /// „Przy celu 1 500 zł miesięcznie resztę rezerwacji uzbierasz do maja" — zdanie z makiety,
    /// i jedyne miejsce, w którym karta celu i karta rezerwacji przestają być sąsiadami
    /// z przypadku.
    /// </summary>
    /// <remarks>
    /// <c>null</c> bez celu: bez tempa nie ma z czego liczyć terminu, a zgadnięcie go byłoby
    /// obietnicą bez pokrycia.
    /// </remarks>
    private async Task<DateOnly?> CoveredByAsync(
        IReadOnlyList<Guid> budgetIds, decimal missing, DateOnly currentMonth, CancellationToken ct)
    {
        if (missing <= 0) return null;

        var monthlyGoal = await db.SavingsGoals
            .Where(g => budgetIds.Contains(g.BudgetBusinessId) && g.EndedOn == null)
            .SumAsync(g => (decimal?)g.Amount, ct) ?? 0m;

        if (monthlyGoal <= 0) return null;

        var months = (int)Math.Ceiling(missing / monthlyGoal);
        return currentMonth.AddMonths(months);
    }

    /// <summary>
    /// Stan konta oszczędnościowego — fallback: wpłaty minus wypłaty w kategorii „Oszczędności".
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Netto jest tu POPRAWNE, w odróżnieniu od werdyktu celu.</b> Tam wypłata nie mogła
    /// psuć dowodu, bo dowód mierzy dyscyplinę wpłacania. Tu mierzymy, ile pieniędzy LEŻY
    /// na koncie — a wypłacone naprawdę z niego zeszły. Dwie liczby, dwie miary, żadnej
    /// niespójności.
    ///
    /// Przelew NA oszczędnościowe wychodzi z konta bieżącego, więc w bazie jest ujemny.
    /// Negacja poza zapytaniem — EF nie tłumaczy <c>-x.Sum(...)</c> w projekcji.
    /// </remarks>
    private async Task<decimal> AccountBalanceAsync(IReadOnlyList<Guid> budgetIds, CancellationToken ct)
    {
        var savingsCategoryId = await savingsCategory.IdAsync(ct);
        if (savingsCategoryId is null) return 0m;

        var signed = await db.Transactions
            .Where(t => t.BudgetBusinessId != null && budgetIds.Contains(t.BudgetBusinessId.Value)
                        && t.CategoryId == savingsCategoryId)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        return -signed;
    }
}
