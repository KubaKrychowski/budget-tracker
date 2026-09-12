using BudgetTracker.Api.Domain;
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
///
/// Samo dopasowanie mieszka w <see cref="RuleMatcher"/>, bo dzieli je z podglądem trafień w kreatorze.
/// </remarks>
public sealed class RuleCategorizer(AppDbContext db) : ICategorizer
{
    private List<CompiledRule>? _rules;

    /// <inheritdoc />
    /// <remarks>
    /// Pierwsza pasująca reguła wygrywa (kolejność: priorytet, potem <c>Id</c>), czyli NIŻSZY numer
    /// priorytetu jest sprawdzany wcześniej. Oba wzorce są opcjonalne, ale ustawione muszą pasować JEDNOCZEŚNIE.
    /// </remarks>
    public async Task<CategorySuggestion> CategorizeAsync(
        string normalizedDescription, string transactionType, decimal amount, CancellationToken ct)
    {
        _rules ??= await LoadRulesAsync(db, ct);

        foreach (var rule in _rules)
        {
            if (RuleMatcher.Matches(rule, normalizedDescription, transactionType, amount))
            {
                return CategorySuggestion.FromRule(rule.CategoryId);
            }
        }

        return CategorySuggestion.None;
    }

    /// <summary>Reguły z bazy, skompilowane, w kolejności sprawdzania.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Reguła bez ani jednego wzorca pasowałaby do każdej transakcji po swojej stronie przepływu.
    /// To zawsze pomyłka w danych, więc jest pomijana zamiast wykonywana.</item>
    /// <item>Zły wzorzec w bazie nie może wywalić całego importu — reguła jest pomijana (wyjątek celowo
    /// połknięty: reguła jest danymi, nie kodem). Transakcja trafi wtedy do modelu albo do przeglądu.</item>
    /// </list>
    /// </remarks>
    public static async Task<List<CompiledRule>> LoadRulesAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.Set<CategoryRule>()
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Id)
            .Select(r => new
            {
                r.Priority, r.Pattern, r.TransactionTypePattern, r.Direction,
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
                    r.Priority,
                    RuleMatcher.Compile(r.Pattern), RuleMatcher.Compile(r.TransactionTypePattern), r.Direction,
                    r.CategoryId, r.MinAmount, r.MaxAmount));
            }
            catch (ArgumentException)
            {
            }
        }

        return compiled;
    }
}
