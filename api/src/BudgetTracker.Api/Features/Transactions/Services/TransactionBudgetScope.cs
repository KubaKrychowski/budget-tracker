using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Services;

/// <summary>
/// Zasięg budżetów ekranu transakcji: lista do multiselecta, rozstrzygnięcie, które budżety liczymy,
/// i transakcje, które do nich należą. Wspólne dla listy, edycji i akcji masowych.
/// </summary>
public sealed class TransactionBudgetScope(AppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Dzisiejsza data — punkt odniesienia wyboru budżetu domyślnego, gdy filtr nie ma daty końcowej.
    /// Z <see cref="TimeProvider"/>, żeby test mógł ją podmienić.
    /// </summary>
    public DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);

    /// <summary>
    /// Budżety do multiselecta nad tabelą, w tej samej kolejności, w jakiej rozstrzyga się
    /// domyślny: od najnowszego miesiąca.
    /// </summary>
    /// <remarks>
    /// Wyłączonych NIE pomijamy — historię ogląda się także po zamknięciu budżetu (CLAUDE.md §5),
    /// więc trafiają na listę z tagiem.
    /// </remarks>
    public async Task<IReadOnlyList<TransactionBudgetOptionResponseDto>> OptionsAsync(CancellationToken ct) =>
        await db.Budgets
            .OrderByDescending(b => b.Month)
            .ThenByDescending(b => b.Id)
            .Select(b => new TransactionBudgetOptionResponseDto(b.BusinessId, b.Name, b.Month, b.DisabledAt != null))
            .ToListAsync(ct);

    /// <summary>
    /// Które budżety liczymy. Nic nie wskazano = ten sam budżet domyślny co na dashboardzie
    /// (<c>GetDashboardQueryHandler</c>), żeby wejście na listę bez parametru pokazywało
    /// to samo, co dashboard dla tego samego okresu.
    /// </summary>
    /// <remarks>
    /// ⚠️ Wskazany, ale nieznany budżet = 404, nigdy ciche pominięcie. Przy WIELU
    /// identyfikatorach pominięcie jednego byłoby gorsze niż przy jednym: odpowiedź
    /// wyglądałaby na kompletną, tylko brakowałoby w niej pieniędzy.
    ///
    /// Liczone w pamięci, na liście pobranej raz — budżetów są jednostki, więc osobne
    /// zapytanie na każdy identyfikator byłoby zapytaniem na darmo.
    /// </remarks>
    public static IReadOnlyList<Guid> Resolve(
        IReadOnlyList<TransactionBudgetOptionResponseDto> budgets,
        IReadOnlyList<Guid>? requested,
        DateOnly referenceDate) =>
        BudgetScope.Resolve(
            [.. budgets.Select(b => new BudgetCandidate(b.Id, b.Month))], requested, referenceDate);

    /// <summary>Transakcje należące do WSKAZANYCH budżetów.</summary>
    /// <remarks>
    /// <c>BudgetBusinessId</c> jest zwykłą kolumną bez relacji (patrz <see cref="Transaction.BudgetBusinessId"/>),
    /// więc dopasowanie idzie po wartości, nie po kluczu obcym. Warunek na <c>null</c> jest
    /// konieczny: transakcje dodane ręcznie i te sprzed wprowadzenia pola budżetu go nie mają.
    /// </remarks>
    public IQueryable<Transaction> TransactionsOf(IReadOnlyList<Guid> budgetIds) =>
        db.Transactions.Where(t => t.BudgetBusinessId != null && budgetIds.Contains(t.BudgetBusinessId.Value));
}
