using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>
/// Wybór budżetów ekranów oszczędności — ta sama reguła (<see cref="BudgetScope"/>) co na dashboardzie
/// i liście transakcji, żeby wszystkie ekrany domyślnie pokazywały ten sam budżet.
/// </summary>
public sealed class SavingsBudgetScope(AppDbContext db, TimeProvider clock)
{
    /// <summary>Dzisiejsza data — punkt odniesienia budżetu domyślnego i bieżącego miesiąca.</summary>
    public DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);

    /// <summary>
    /// Budżety do przełącznika nad ekranem, w tej samej kolejności, w jakiej rozstrzyga się domyślny:
    /// od najnowszego miesiąca.
    /// </summary>
    /// <remarks>
    /// Bez tej listy ekrany oszczędności nie miały własnego wyboru budżetu — zmienić go dało się tylko
    /// wchodząc z dashboardu albo przez adres. Wyłączonych NIE pomijamy (CLAUDE.md §5).
    /// </remarks>
    public async Task<IReadOnlyList<SavingsBudgetOptionResponseDto>> OptionsAsync(CancellationToken ct) =>
        await db.Budgets
            .OrderByDescending(b => b.Month)
            .ThenByDescending(b => b.Id)
            .Select(b => new SavingsBudgetOptionResponseDto(b.BusinessId, b.Name, b.Month, b.DisabledAt != null))
            .ToListAsync(ct);

    /// <summary>
    /// Budżety, na których liczy się odczyt: wskazane albo jeden domyślny — rozstrzygane na liście już pobranej
    /// do przełącznika, bez drugiego zapytania o budżety.
    /// </summary>
    /// <remarks>Pusta lista tylko wtedy, gdy w bazie nie ma żadnego budżetu. Nieznany wskazany = 404.</remarks>
    public static IReadOnlyList<Guid> Resolve(
        IReadOnlyList<SavingsBudgetOptionResponseDto> budgets, IReadOnlyList<Guid>? requested, DateOnly today) =>
        BudgetScope.Resolve([.. budgets.Select(b => new BudgetCandidate(b.Id, b.Month))], requested, today);

    /// <summary>Budżet, którego dotyczy zapis — wskazany albo domyślny.</summary>
    /// <remarks>Zapis dotyczy jednego budżetu — brak jakiegokolwiek budżetu to 404, nie cicha pustka.</remarks>
    public async Task<Guid> SingleAsync(Guid? requested, DateOnly today, CancellationToken ct)
    {
        var resolved = BudgetScope
            .Resolve(await CandidatesAsync(ct), requested is { } one ? [one] : null, today)
            .FirstOrDefault();

        return resolved == default ? throw new BudgetNotFoundException(requested ?? Guid.Empty) : resolved;
    }

    private async Task<IReadOnlyList<BudgetCandidate>> CandidatesAsync(CancellationToken ct) =>
        await db.Budgets
            .OrderByDescending(b => b.Month)
            .ThenByDescending(b => b.Id)
            .Select(b => new BudgetCandidate(b.BusinessId, b.Month))
            .ToListAsync(ct);
}
