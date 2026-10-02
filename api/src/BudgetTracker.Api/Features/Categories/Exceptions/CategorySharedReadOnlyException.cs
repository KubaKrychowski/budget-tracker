namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>Kategoria wspólna (bazowa) jest tylko do odczytu: próba jej zmiany albo usunięcia. Warstwa HTTP tłumaczy to na 409.</summary>
public sealed class CategorySharedReadOnlyException(Guid businessId)
    : Exception($"Kategoria {businessId} jest wspólna dla wszystkich kont i nie można jej zmienić ani usunąć.");
