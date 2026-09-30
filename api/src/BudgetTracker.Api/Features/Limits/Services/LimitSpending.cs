using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Limits.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Limits.Services;

/// <summary>Ile w okresie „poszło" z limitu każdej kategorii — wydatki plus pieniądze zarezerwowane na cele ze zwykłego konta.</summary>
/// <remarks>
/// <para>
/// Jedno miejsce liczenia dla ekranu limitów i podglądu w oknie wpłaty, żeby oba pokazywały tę samą liczbę.
/// </para>
/// <para>
/// ⚠️ Wpłata na cel ze zwykłego konta (<see cref="ContributionSource.Regular"/>) nie jest transakcją, więc jej
/// dokłada się osobno, po dacie wpłaty. Liczą się tylko rezerwacje NIEROZLICZONE: rozliczenie zakupem tworzy prawdziwą
/// transakcję w swojej kategorii, a wpłata liczona dalej policzyłaby te same pieniądze drugi raz.
/// </para>
/// </remarks>
public sealed class LimitSpending(AppDbContext db)
{
    /// <summary>Klucz słownika wydatków dla transakcji bez kategorii — kategorie w bazie mają klucze dodatnie.</summary>
    public const int UncategorizedKey = 0;

    /// <summary>Wydatki okresu per kategoria — dodatnie, licznik transakcji obok.</summary>
    /// <remarks>
    /// Negacja POZA zapytaniem — EF nie tłumaczy <c>-g.Sum(...)</c> w projekcji (ta sama pułapka co w
    /// <c>GetDashboardQueryHandler</c>). Transakcje bez kategorii lądują pod <see cref="UncategorizedKey"/>.
    /// </remarks>
    public async Task<Dictionary<int, CategorySpend>> ByCategoryAsync(
        Guid budget, DateOnly from, DateOnly end, CancellationToken ct)
    {
        var rows = await db.Transactions
            .Where(t => t.BudgetBusinessId == budget && t.Date >= from && t.Date < end && t.Amount < 0)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var spent = rows.ToDictionary(r => r.Key ?? UncategorizedKey, r => new CategorySpend(-r.Total, r.Count));

        var reserved = await RegularContributionsAsync(budget, from, end, ct);
        if (reserved.Count == 0) return spent;

        var ids = await db.Categories
            .Where(c => reserved.Keys.Contains(c.BusinessId))
            .Select(c => new { c.Id, c.BusinessId })
            .ToListAsync(ct);

        foreach (var category in ids)
        {
            var current = spent.GetValueOrDefault(category.Id) ?? new CategorySpend(0m, 0);
            spent[category.Id] = current with { Spent = current.Spent + reserved[category.BusinessId] };
        }

        return spent;
    }

    /// <summary>Wpłaty ze zwykłego konta z datą w okresie, zsumowane po kategorii (publiczny identyfikator).</summary>
    /// <remarks>Suma w pamięci: wpłaty siedzą w kolumnie jsonb, a nierozliczonych rezerwacji w budżecie jest kilka.</remarks>
    private async Task<Dictionary<Guid, decimal>> RegularContributionsAsync(
        Guid budget, DateOnly from, DateOnly end, CancellationToken ct)
    {
        var reservations = await db.SavingsReservations
            .Where(r => r.BudgetBusinessId == budget && r.SettledAt == null)
            .ToListAsync(ct);

        return reservations
            .SelectMany(r => r.Contributions)
            .Where(c => c.Source == ContributionSource.Regular && c.CategoryBusinessId != null
                        && c.Date >= from && c.Date < end)
            .GroupBy(c => c.CategoryBusinessId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));
    }
}
