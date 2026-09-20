namespace BudgetTracker.Api.Features.Categorization.Exceptions;

/// <summary>
/// Reguła wspólna (bazowa) jest tylko do odczytu: próba jej zmiany albo usunięcia przez zwykłego użytkownika.
/// Warstwa HTTP tłumaczy to na 409 — żądanie jest poprawne, tylko stan zasobu na nie nie pozwala.
/// </summary>
public sealed class CategoryRuleSharedReadOnlyException(Guid businessId)
    : Exception($"Reguła {businessId} jest wspólna dla wszystkich kont i nie można jej zmienić ani usunąć.");
