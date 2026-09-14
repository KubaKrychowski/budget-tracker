using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.EpisodicOrders.Services;

/// <summary>
/// Wybór budżetu ekranu zleceń epizodycznych — ta sama reguła (<see cref="BudgetScope"/>) co na pozostałych ekranach,
/// żeby domyślnie wszystkie pokazywały ten sam budżet.
/// </summary>
public sealed class EpisodicOrdersBudgetScope(AppDbContext db, TimeProvider clock)
{
    /// <summary>Pierwszy dzień bieżącego miesiąca — z <see cref="TimeProvider"/>, żeby test mógł go podmienić.</summary>
    public DateOnly CurrentMonth()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);
        return new DateOnly(today.Year, today.Month, 1);
    }

    public DateTimeOffset Now() => clock.GetUtcNow();

    /// <summary>Budżety do przełącznika, w kolejności rozstrzygania domyślnego: od najnowszego miesiąca.</summary>
    public async Task<IReadOnlyList<EpisodicOrdersBudgetOptionResponseDto>> OptionsAsync(CancellationToken ct) =>
        await db.Budgets
            .OrderByDescending(b => b.Month)
            .ThenByDescending(b => b.Id)
            .Select(b => new EpisodicOrdersBudgetOptionResponseDto(b.BusinessId, b.Name, b.Month, b.DisabledAt != null))
            .ToListAsync(ct);

    /// <summary>JEDEN budżet ekranu: wskazany albo domyślny; <c>null</c> tylko wtedy, gdy nie ma żadnego. Nieznany = 404.</summary>
    public static Guid? Resolve(
        IReadOnlyList<EpisodicOrdersBudgetOptionResponseDto> budgets, Guid? requested, DateOnly referenceDate)
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
