namespace BudgetTracker.Api.Features.Admin.Exceptions;

/// <summary>Docelowy właściciel jest pusty albo taki sam jak źródłowy — przepisanie nie ma sensu. Warstwa HTTP tłumaczy to na 400.</summary>
public sealed class OwnerReassignInvalidException(Guid sourceOwnerId, Guid targetUserId)
    : Exception($"Nie można przepisać danych właściciela {sourceOwnerId} na {targetUserId}.");
