using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Services;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Queries;

/// <summary>
/// Lista transakcji z filtrowaniem, sortowaniem i stronicowaniem po stronie serwera, razem z kaflami podsumowania.
/// </summary>
public sealed class GetTransactionsListQueryHandler(
    TransactionBudgetScope scope, TransactionFilters filters, TransactionListItemReader reader)
{
    /// <summary>Górna granica strony — patrz <see cref="ClampPageSize"/>.</summary>
    private const int MaxPageSize = 200;

    /// <summary>Strona listy dla <paramref name="filter"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Brak budżetów w bazie to pierwszy z trzech pustych stanów — odpowiedź bez zapytań o transakcje.</item>
    /// <item><see cref="TransactionListResponseDto.HasAnyTransactions"/> liczone celowo PRZED filtrami: rozstrzyga
    /// „budżet bez transakcji" (drugi pusty stan) niezależnie od tego, czy filtr coś zawęża (trzeci pusty stan).</item>
    /// <item>Kafle liczone z przefiltrowanego zestawu PRZED <c>Skip</c>/<c>Take</c> — patrz <see cref="TransactionSummaryResponseDto"/>.</item>
    /// </list>
    /// </remarks>
    public async Task<TransactionListResponseDto> HandleAsync(
        TransactionFilterRequestDto filter, int page, int pageSize, string? sort, bool desc, CancellationToken ct)
    {
        page = ClampPage(page);
        pageSize = ClampPageSize(pageSize);

        var budgets = await scope.OptionsAsync(ct);
        var selectedBudgetIds = TransactionBudgetScope.Resolve(budgets, filter.BudgetIds, filter.To ?? scope.Today());

        if (selectedBudgetIds.Count == 0)
        {
            return new TransactionListResponseDto(
                [], 0, page, pageSize, TransactionSummaryResponseDto.Empty, [], budgets, false);
        }

        var ofBudget = scope.TransactionsOf(selectedBudgetIds);
        var hasAny = await ofBudget.AnyAsync(ct);

        var filtered = filters.Apply(ofBudget, filter);
        var total = await filtered.CountAsync(ct);
        var summary = await BuildSummaryAsync(filtered, ct);

        var items = await reader
            .Project(ApplySort(filtered, sort, desc).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(ct);

        return new TransactionListResponseDto(
            items, total, page, pageSize, summary, selectedBudgetIds, budgets, hasAny);
    }

    /// <summary>
    /// Strona i jej rozmiar MUSZĄ być przycięte, zanim dotkną zapytania.
    /// </summary>
    /// <remarks>
    /// Nie jest to kurtuazja wobec złośliwego klienta, tylko warunek działania: `page = 0` daje `Skip(-pageSize)`,
    /// a Postgres na ujemny OFFSET odpowiada „OFFSET must not be negative" — wyjątek, którego
    /// <c>DomainExceptionHandler</c> nie zna, czyli 500. A adres tego ekranu jest z założenia
    /// do kopiowania i ręcznego poprawiania (filtry żyją w URL), więc `?page=0` to nie atak,
    /// tylko backspace o jeden za dużo.
    ///
    /// Górna granica jest z tego samego powodu co dolna: `pageSize=1000000` nie wywala
    /// zapytania, ale każe policzyć i odesłać cały budżet (tu: ponad 1300 wierszy) w JSON-ie,
    /// żeby narysować jedną stronę tabeli.
    /// </remarks>
    private static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);

    private static int ClampPage(int page) => Math.Max(1, page);

    /// <summary>
    /// Ta sama arytmetyka co na dashboardzie — tylko zasięgiem nie jest okno dat
    /// budżetu, a bieżący filtr tabeli.
    /// </summary>
    /// <remarks>
    /// Rozjazd między tymi dwoma miejscami byłby dla użytkownika nieodróżnialny od błędu w danych.
    /// Znak decyduje o kierunku: ujemne = wydatek, dodatnie = przychód (<see cref="Transaction.Amount"/>);
    /// wydatki raportujemy jako wartość dodatnią, bo tak są prezentowane w UI. Najmniejsza kwota
    /// = największy wydatek, bo wydatki są ujemne.
    /// </remarks>
    private static async Task<TransactionSummaryResponseDto> BuildSummaryAsync(
        IQueryable<Transaction> filtered, CancellationToken ct)
    {
        var expenses = filtered.Where(t => t.Amount < 0);

        var totalExpenses = -await expenses.SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        var totalIncome = await filtered.Where(t => t.Amount > 0)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        var largestExpense = await expenses.MinAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        return new TransactionSummaryResponseDto(
            totalExpenses,
            totalIncome,
            Balance: totalIncome - totalExpenses,
            LargestExpenseAmount: -largestExpense);
    }

    /// <summary>
    /// Sortowanie MUSI mieć tie-break po <c>Id</c> — sama data przy powtarzalnych wartościach
    /// daje niestabilną kolejność między stronami (ten sam wiersz na dwóch stronach naraz).
    /// </summary>
    private static IQueryable<Transaction> ApplySort(IQueryable<Transaction> query, string? sort, bool desc)
    {
        var byAmount = string.Equals(sort, "amount", StringComparison.OrdinalIgnoreCase);
        return (byAmount, desc) switch
        {
            (true, true) => query.OrderByDescending(t => t.Amount).ThenByDescending(t => t.Id),
            (true, false) => query.OrderBy(t => t.Amount).ThenBy(t => t.Id),
            (false, true) => query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id),
            (false, false) => query.OrderBy(t => t.Date).ThenBy(t => t.Id),
        };
    }
}
