using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Search.Consts;
using BudgetTracker.Api.Features.Search.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Search.Queries;

/// <summary>
/// Wyszukiwarka z nagłówka: jedno żądanie, wyniki pogrupowane po rodzaju.
/// </summary>
/// <remarks>
/// <para>
/// Szuka po NAZWACH i OPISACH, zawsze „zawiera" i bez rozróżniania wielkości liter. Fraza idzie przez
/// <see cref="LikePattern"/>, więc procent i podkreślnik z wyciągu bankowego nie zamieniają się
/// w metaznaki wzorca (klasyczna pułapka: „5%" znajduje „ODSETKI 15 PLN").
/// </para>
/// <para>
/// ⚠️ Zakres to JEDEN budżet, nie wszystkie. Na koncie oszczędnościowym leży osobny budżet, więc
/// domyślne przeszukiwanie wszystkiego mieszałoby konto bieżące z oszczędnościowym w jednej liście —
/// a to dwie różne kieszenie. Poszerzenie jest świadomą akcją użytkownika (<c>allBudgets</c>).
/// Kategorie są wyjątkiem: należą do właściciela, nie do budżetu, więc nie zawężamy ich nigdy.
/// </para>
/// <para>
/// ⚠️ Osobne <c>Count</c> obok <c>Take</c> jest celowe. Menu pokazuje podgląd kilku trafień, ale musi
/// powiedzieć, ILE ich jest naprawdę — inaczej trzy widoczne transakcje wyglądają jak komplet i użytkownik
/// przestaje szukać, choć jego pozycja jest dziewiąta.
/// </para>
/// <para>
/// Bez indeksu po opisie: przy kilku tysiącach transakcji sekwencyjny skan kosztuje milisekundy, a indeks
/// dla wzorca „%fraza%" wymagałby rozszerzenia <c>pg_trgm</c> — czyli zależności instalacyjnej, której nie
/// chcemy przed pierwszym pomiarem na produkcji.
/// </para>
/// </remarks>
public sealed class GetSearchResultsQueryHandler(AppDbContext db, TimeProvider clock)
{
    /// <summary>Krótsza fraza pasuje do wszystkiego i menu staje się losową listą.</summary>
    public const int MinQueryLength = 2;

    /// <summary>Ile trafień jednej grupy wchodzi do menu. Reszta czeka za „Pokaż wszystkie wyniki”.</summary>
    public const int HitsPerGroup = 5;

    public async Task<SearchResponseDto> HandleAsync(
        string? query, Guid? budgetId, bool allBudgets, CancellationToken ct)
    {
        var q = (query ?? "").Trim();
        if (q.Length < MinQueryLength) return new SearchResponseDto(q, [], 0, null, null, allBudgets);

        var like = LikePattern.Contains(q);
        var (scopeId, scopeName) = allBudgets ? (null, null) : await ResolveBudgetAsync(budgetId, ct);

        var groups = new List<SearchGroupResponseDto>();
        foreach (var kind in SearchKind.Order)
        {
            var group = await GroupAsync(kind, like, scopeId, ct);
            if (group.Total > 0) groups.Add(group);
        }

        return new SearchResponseDto(
            Query: q,
            Groups: groups,
            Total: groups.Sum(g => g.Total),
            BudgetId: scopeId,
            BudgetName: scopeName,
            AllBudgets: allBudgets);
    }

    /// <summary>Budżet zapytania: wskazany albo domyślny, tą samą regułą co pozostałe ekrany.</summary>
    /// <remarks>Nieznany wskazany to 404, nigdy ciche przejście na domyślny — inaczej wyniki byłyby z innego budżetu.</remarks>
    private async Task<(Guid?, string?)> ResolveBudgetAsync(Guid? requested, CancellationToken ct)
    {
        var candidates = await db.Budgets
            .OrderByDescending(b => b.Month).ThenByDescending(b => b.Id)
            .Select(b => new { b.BusinessId, b.Name, b.Month })
            .ToListAsync(ct);

        if (candidates.Count == 0) return (null, null);

        var resolved = BudgetScope.Resolve(
            [.. candidates.Select(c => new BudgetCandidate(c.BusinessId, c.Month))],
            requested is { } one ? [one] : null,
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date));

        if (resolved.Count == 0) throw new BudgetNotFoundException(requested ?? Guid.Empty);

        var picked = candidates.First(c => c.BusinessId == resolved[0]);
        return (picked.BusinessId, picked.Name);
    }

    private Task<SearchGroupResponseDto> GroupAsync(string kind, string like, Guid? scopeId, CancellationToken ct) =>
        kind switch
        {
            SearchKind.Categories => CategoriesAsync(like, ct),
            SearchKind.Budgets => BudgetsAsync(like, ct),
            SearchKind.StandingOrders => StandingOrdersAsync(like, scopeId, ct),
            SearchKind.EpisodicOrders => EpisodicOrdersAsync(like, scopeId, ct),
            SearchKind.Transactions => TransactionsAsync(like, scopeId, ct),
            _ => Task.FromResult(new SearchGroupResponseDto(kind, 0, [])),
        };

    private async Task<SearchGroupResponseDto> CategoriesAsync(string like, CancellationToken ct)
    {
        var query = db.Categories.Where(c => EF.Functions.ILike(c.Name, like, LikePattern.EscapeChar));
        var total = await query.CountAsync(ct);
        var hits = await query
            .OrderBy(c => c.Name)
            .Take(HitsPerGroup)
            .Select(c => new SearchHitResponseDto(c.BusinessId, c.Name, null, null, null, null, null))
            .ToListAsync(ct);
        return new SearchGroupResponseDto(SearchKind.Categories, total, hits);
    }

    private async Task<SearchGroupResponseDto> BudgetsAsync(string like, CancellationToken ct)
    {
        var query = db.Budgets.Where(b => EF.Functions.ILike(b.Name, like, LikePattern.EscapeChar));
        var total = await query.CountAsync(ct);
        var hits = await query
            .OrderByDescending(b => b.Month)
            .Take(HitsPerGroup)
            .Select(b => new SearchHitResponseDto(b.BusinessId, b.Name, null, null, null, b.BusinessId, b.Name))
            .ToListAsync(ct);
        return new SearchGroupResponseDto(SearchKind.Budgets, total, hits);
    }

    private async Task<SearchGroupResponseDto> StandingOrdersAsync(string like, Guid? scopeId, CancellationToken ct)
    {
        var query = db.StandingOrders
            .Where(o => EF.Functions.ILike(o.Name, like, LikePattern.EscapeChar))
            .Where(o => scopeId == null || o.BudgetBusinessId == scopeId);
        var total = await query.CountAsync(ct);
        var hits = await query
            .OrderBy(o => o.Name)
            .Take(HitsPerGroup)
            .Select(o => new SearchHitResponseDto(
                o.BusinessId, o.Name, null, null, o.ExpectedAmount, o.BudgetBusinessId,
                db.Budgets.Where(b => b.BusinessId == o.BudgetBusinessId).Select(b => b.Name).FirstOrDefault()))
            .ToListAsync(ct);
        return new SearchGroupResponseDto(SearchKind.StandingOrders, total, hits);
    }

    private async Task<SearchGroupResponseDto> EpisodicOrdersAsync(string like, Guid? scopeId, CancellationToken ct)
    {
        var query = db.EpisodicOrders
            .Where(o => EF.Functions.ILike(o.Name, like, LikePattern.EscapeChar)
                || (o.Description != null && EF.Functions.ILike(o.Description, like, LikePattern.EscapeChar)))
            .Where(o => scopeId == null || o.BudgetBusinessId == scopeId);
        var total = await query.CountAsync(ct);
        var hits = await query
            .OrderByDescending(o => o.DueMonth)
            .Take(HitsPerGroup)
            .Select(o => new SearchHitResponseDto(
                o.BusinessId, o.Name, null,
                o.DueMonth, o.PlannedAmount, o.BudgetBusinessId,
                db.Budgets.Where(b => b.BusinessId == o.BudgetBusinessId).Select(b => b.Name).FirstOrDefault()))
            .ToListAsync(ct);
        return new SearchGroupResponseDto(SearchKind.EpisodicOrders, total, hits);
    }

    /// <summary>Transakcje — od najnowszej, bo szukając „biedronka" pyta się o ostatni zakup, nie o pierwszy.</summary>
    private async Task<SearchGroupResponseDto> TransactionsAsync(string like, Guid? scopeId, CancellationToken ct)
    {
        var query = db.Transactions
            .Where(t => EF.Functions.ILike(t.Description, like, LikePattern.EscapeChar))
            .Where(t => scopeId == null || t.BudgetBusinessId == scopeId);
        var total = await query.CountAsync(ct);
        var hits = await query
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Take(HitsPerGroup)
            .Select(t => new SearchHitResponseDto(
                t.BusinessId,
                t.Description,
                db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefault(),
                t.Date,
                t.Amount,
                t.BudgetBusinessId,
                db.Budgets.Where(b => b.BusinessId == t.BudgetBusinessId).Select(b => b.Name).FirstOrDefault()))
            .ToListAsync(ct);
        return new SearchGroupResponseDto(SearchKind.Transactions, total, hits);
    }
}
