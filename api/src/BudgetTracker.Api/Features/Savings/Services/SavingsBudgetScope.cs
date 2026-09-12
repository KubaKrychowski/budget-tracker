using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;
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

    /// <summary>Budżety, na których liczy się odczyt: wskazane albo jeden domyślny.</summary>
    /// <remarks>Pusta lista tylko wtedy, gdy w bazie nie ma żadnego budżetu. Nieznany wskazany = 404.</remarks>
    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        IReadOnlyList<Guid>? requested, DateOnly today, CancellationToken ct) =>
        BudgetScope.Resolve(await CandidatesAsync(ct), requested, today);

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
