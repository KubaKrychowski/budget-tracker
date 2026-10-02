namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>Kategoria przychodowa wybrana dla transakcji o kwocie ujemnej. Warstwa HTTP tłumaczy to na 400.</summary>
/// <remarks>Odwrotnie nie obowiązuje: na kategorię wydatkową wolno przypisać wpływ, bo tak wygląda zwrot zakupu.</remarks>
public sealed class CategoryTypeMismatchException : Exception;
