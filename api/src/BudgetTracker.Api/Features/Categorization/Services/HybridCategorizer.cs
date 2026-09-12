using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Podejście hybrydowe z CLAUDE.md §3: reguły + ML, w tej kolejności.
/// </summary>
/// <remarks>
/// Reguły idą pierwsze nie dlatego, że są szybsze, tylko dlatego, że są PEWNE.
/// Dopasowanie „jmp s.a." do Jedzenia nie jest predykcją i nie ma powodu, żeby model
/// mógł je podważyć ani żeby taka transakcja trafiała do przeglądu przez niski próg.
///
/// Model dostaje wyłącznie to, czego reguły nie złapały — czyli długi ogon sprzedawców,
/// których nie da się wypisać ręcznie.
///
/// ⚠️ Model zna WYŁĄCZNIE kategorie wydatkowe — w zbiorze treningowym nie ma ani jednego
/// wpływu. Zapytany o wynagrodzenie i tak musi wskazać którąś z nich i robi to
/// z pewnością bliską 1.0, więc taka transakcja nie trafiłaby nawet do przeglądu.
/// Wpływy obsługują same reguły; brak dopasowania = brak kategorii, nie zgadywanie.
/// </remarks>
public sealed class HybridCategorizer(RuleCategorizer rules, MlCategorizer ml) : ICategorizer
{
    public async Task<CategorySuggestion> CategorizeAsync(
        string normalizedDescription, string transactionType, decimal amount, CancellationToken ct)
    {
        var byRule = await rules.CategorizeAsync(normalizedDescription, transactionType, amount, ct);
        if (byRule.CategoryId is not null) return byRule;

        if (amount > 0) return CategorySuggestion.None;

        return await ml.CategorizeAsync(normalizedDescription, transactionType, amount, ct);
    }
}
