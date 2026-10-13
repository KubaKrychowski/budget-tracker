namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>Próba ukrycia kategorii własnej — ukrywa się tylko wspólne; własną można przemianować albo usunąć.</summary>
public sealed class CategoryHideNotSharedException(Guid businessId) : Exception($"Kategoria {businessId} jest własna, a ukrywać można tylko wspólne.");
