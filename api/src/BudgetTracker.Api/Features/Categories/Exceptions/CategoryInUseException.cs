namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>
/// Kategoria ma transakcje, limity, reguły albo zlecenia, więc nie wolno jej skasować. Warstwa HTTP tłumaczy to na 409.
/// </summary>
/// <remarks>
/// Skasowanie logiczne nie złamałoby kluczy obcych, ale zostawiłoby transakcje z kategorią, której nie ma
/// na żadnej liście wyboru.
/// </remarks>
public sealed class CategoryInUseException : Exception;
