using System.Text.RegularExpressions;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Kategoryzacja regułami — pierwsza linia, przed modelem.
/// </summary>
/// <remarks>
/// Reguły łapią przypadki, których model nie musi się uczyć, i rozwiązują problem zimnego
/// startu: przy pustej bazie nie ma danych treningowych, więc bez reguł pierwszy import
/// trafiłby w całości do przeglądu (CLAUDE.md §3).
///
/// Reguły są ładowane raz na instancję i trzymane w pamięci — import to setki
/// wierszy, a odpytywanie bazy per wiersz byłoby N+1.
///
/// ⚠️ Cache reguł NIE wymaga unieważniania, bo klasa jest rejestrowana jako <c>Scoped</c>: pamięta
/// reguły wyłącznie w obrębie jednego żądania, a następne dostaje nową instancję i czyta bazę od nowa.
/// Gdyby kiedyś ktoś zmienił rejestrację na <c>Singleton</c> „dla wydajności", reguła dodana przez API
/// przestanie działać do restartu — i będzie to wyglądać na błąd zapisu.
/// </remarks>
public sealed class RuleCategorizer(AppDbContext db) : ICategorizer
{
    /// <summary>
    /// Wzorce pochodzą z bazy, więc teoretycznie mogą być kosztowne. Timeout chroni przed
    /// zawieszeniem importu na patologicznym wyrażeniu (ReDoS).
    /// </summary>
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private List<CompiledRule>? _rules;

    /// <summary>Reguła gotowa do dopasowania — wzorce skompilowane raz, przy wczytaniu.</summary>
    private sealed record CompiledRule(
        Regex? Pattern, Regex? TypePattern, RuleDirection Direction,
        int CategoryId, decimal? Min, decimal? Max);

    /// <inheritdoc />
    /// <remarks>
    /// Pierwsza pasująca reguła wygrywa (kolejność: priorytet, potem <c>Id</c>). Oba wzorce są opcjonalne,
    /// ale ustawione muszą pasować JEDNOCZEŚNIE.
    /// </remarks>
    public async Task<CategorySuggestion> CategorizeAsync(
        string normalizedDescription, string transactionType, decimal amount, CancellationToken ct)
    {
        _rules ??= await LoadRulesAsync(ct);

        var abs = Math.Abs(amount);
        foreach (var rule in _rules)
        {
            if (!DirectionAllows(rule.Direction, amount)) continue;
            if (rule.Min is { } min && abs < min) continue;
            if (rule.Max is { } max && abs >= max) continue;

            if (rule.Pattern is { } p && !p.IsMatch(normalizedDescription)) continue;
            if (rule.TypePattern is { } t && !t.IsMatch(transactionType)) continue;

            return CategorySuggestion.FromRule(rule.CategoryId);
        }

        return CategorySuggestion.None;
    }

    private static bool DirectionAllows(RuleDirection direction, decimal amount) => direction switch
    {
        RuleDirection.Expense => amount < 0,
        RuleDirection.Income => amount > 0,
        _ => true,
    };

    /// <summary>Reguły z bazy, skompilowane, w kolejności sprawdzania.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Reguła bez ani jednego wzorca pasowałaby do każdej transakcji po swojej stronie przepływu.
    /// To zawsze pomyłka w danych, więc jest pomijana zamiast wykonywana.</item>
    /// <item>Zły wzorzec w bazie nie może wywalić całego importu — reguła jest pomijana (wyjątek celowo
    /// połknięty: reguła jest danymi, nie kodem). Transakcja trafi wtedy do modelu albo do przeglądu.</item>
    /// </list>
    /// </remarks>
    private async Task<List<CompiledRule>> LoadRulesAsync(CancellationToken ct)
    {
        var rows = await db.Set<CategoryRule>()
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Id)
            .Select(r => new
            {
                r.Pattern, r.TransactionTypePattern, r.Direction,
                r.CategoryId, r.MinAmount, r.MaxAmount,
            })
            .ToListAsync(ct);

        var compiled = new List<CompiledRule>(rows.Count);
        foreach (var r in rows)
        {
            if (r.Pattern is null && r.TransactionTypePattern is null) continue;

            try
            {
                compiled.Add(new CompiledRule(
                    Compile(r.Pattern), Compile(r.TransactionTypePattern), r.Direction,
                    r.CategoryId, r.MinAmount, r.MaxAmount));
            }
            catch (ArgumentException)
            {
            }
        }
        return compiled;

        static Regex? Compile(string? pattern) => pattern is null
            ? null
            : new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
    }
}
