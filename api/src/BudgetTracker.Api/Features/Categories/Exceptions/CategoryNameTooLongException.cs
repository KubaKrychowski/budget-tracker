using BudgetTracker.Api.Features.Categories.Services;

namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>Nazwa kategorii jest dłuższa niż <see cref="CategoryNames.MaxLength"/> znaków. Warstwa HTTP tłumaczy to na 400.</summary>
/// <remarks>Osobny wyjątek, bo błąd długości kolumny z bazy nie jest mapowany na żaden kod HTTP i dawałby 500.</remarks>
public sealed class CategoryNameTooLongException : Exception;
