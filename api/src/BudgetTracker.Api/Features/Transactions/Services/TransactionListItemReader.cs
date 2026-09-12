using System.Linq.Expressions;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Services;

/// <summary>Projekcja transakcji na wiersz tabeli — wspólna dla listy i odpowiedzi po edycji.</summary>
public sealed class TransactionListItemReader(AppDbContext db)
{
    /// <summary>Rzutuje zapytanie na wiersze tabeli, po stronie SQL.</summary>
    public IQueryable<TransactionListItemResponseDto> Project(IQueryable<Transaction> query) => query.Select(ProjectToListItem);

    /// <summary>Wiersze o wskazanych identyfikatorach — odpowiedź po zapisie, żeby front nie zgadywał stanu.</summary>
    public async Task<IReadOnlyList<TransactionListItemResponseDto>> ReadByIdsAsync(
        IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await Project(db.Transactions.Where(t => ids.Contains(t.BusinessId))).ToListAsync(ct);

    /// <summary>
    /// Drzewo wyrażenia, nie zwykła metoda — EF musi je przetłumaczyć na SQL
    /// wewnątrz <c>Select</c>, a zwykłego wywołania metody (poza znanymi funkcjami typu
    /// <c>Math.Abs</c>) tłumaczyć nie potrafi.
    /// </summary>
    /// <remarks>
    /// Właściwość instancji, nie pole statyczne: transakcja nie ma nawigacji do kategorii (CLAUDE.md §5),
    /// więc kategorię dociągają podzapytania po <c>db.Categories</c>, a to wymaga kontekstu.
    /// </remarks>
    private Expression<Func<Transaction, TransactionListItemResponseDto>> ProjectToListItem => t => new TransactionListItemResponseDto(
        t.BusinessId,
        t.Date,
        t.Description,
        t.Amount,
        db.Categories.Where(c => c.Id == t.CategoryId).Select(c => (Guid?)c.BusinessId).FirstOrDefault(),
        db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefault(),
        t.Status.ToString(),
        t.IsLargeExpense,
        t.Confidence);
}
