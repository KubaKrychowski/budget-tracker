using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Limits.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Limits.Services;

/// <summary>
/// Wybór budżetu ekranu limitów — ta sama reguła (<see cref="BudgetScope"/>) co na dashboardzie, liście
/// transakcji i ekranach oszczędności, żeby wszystkie ekrany domyślnie pokazywały ten sam budżet.
/// </summary>
public sealed class LimitsBudgetScope(AppDbContext db, TimeProvider clock)
{
    /// <summary>Pierwszy dzień bieżącego miesiąca — z <see cref="TimeProvider"/>, żeby test mógł go podmienić.</summary>
    public DateOnly CurrentMonth()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);
        return new DateOnly(today.Year, today.Month, 1);
    }

    /// <summary>Budżety do przełącznika, w kolejności rozstrzygania domyślnego: od najnowszego miesiąca.</summary>
    public async Task<IReadOnlyList<LimitsBudgetOptionResponseDto>> OptionsAsync(CancellationToken ct) =>
        await db.Budgets
            .OrderByDescending(b => b.Month)
            .ThenByDescending(b => b.Id)
            .Select(b => new LimitsBudgetOptionResponseDto(b.BusinessId, b.Name, b.Month, b.DisabledAt != null))
            .ToListAsync(ct);

    /// <summary>
    /// JEDEN budżet ekranu: wskazany albo domyślny; <c>null</c> tylko wtedy, gdy nie ma żadnego budżetu.
    /// </summary>
    /// <remarks>
    /// Limity są zasadą jednego budżetu — suma limitów dwóch wariantów tych samych danych nie znaczy nic.
    /// Nieznany wskazany = 404, nigdy ciche przejście na domyślny.
    /// </remarks>
    public static Guid? Resolve(
        IReadOnlyList<LimitsBudgetOptionResponseDto> budgets, Guid? requested, DateOnly referenceDate)
    {
        var resolved = BudgetScope.Resolve(
            [.. budgets.Select(b => new BudgetCandidate(b.Id, b.Month))],
            requested is { } one ? [one] : null,
            referenceDate);
        return resolved.Count == 0 ? null : resolved[0];
    }

    /// <summary>Budżet, którego dotyczy zapis — wskazany albo domyślny; brak jakiegokolwiek to 404.</summary>
    public async Task<Guid> SingleAsync(Guid? requested, CancellationToken ct) =>
        Resolve(await OptionsAsync(ct), requested, CurrentMonth())
        ?? throw new BudgetNotFoundException(requested ?? Guid.Empty);
}
