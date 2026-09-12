using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Jeden z dwóch twardych szwów w projekcie (CLAUDE.md §4): reguły dziś, ML jutro,
/// może inny model potem.
/// </summary>
/// <remarks>
/// Implementacja dostaje WYŁĄCZNIE znormalizowany opis i typ transakcji — nie wie,
/// z jakiego banku pochodzi wiersz ani jak wyglądał surowy CSV. To celowe: dzięki temu
/// dołożenie kolejnego parsera nie wymaga dotykania kategoryzacji.
/// </remarks>
public interface ICategorizer
{
    Task<CategorySuggestion> CategorizeAsync(
        string normalizedDescription,
        string transactionType,
        decimal amount,
        CancellationToken ct);
}
