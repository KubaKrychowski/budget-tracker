namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>Zmiana typu kategorii, która jest już używana. Warstwa HTTP tłumaczy to na 409.</summary>
/// <remarks>
/// Wpływ zmieniony na wydatek (albo odwrotnie) zostawiłby w kategorii transakcje o przeciwnym znaku i limit na
/// kategorii przychodowej — stan, który reszta aplikacji uznaje za niemożliwy.
/// </remarks>
public sealed class CategoryTypeLockedException : Exception;
