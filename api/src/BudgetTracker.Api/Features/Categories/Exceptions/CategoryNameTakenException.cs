namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>
/// Kategoria o tej nazwie już istnieje wśród własnych albo wspólnych (wielkość liter nie ma znaczenia).
/// Warstwa HTTP tłumaczy to na 409 — żądanie jest poprawne, tylko stan zasobu na nie nie pozwala.
/// </summary>
public sealed class CategoryNameTakenException : Exception;
