namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>Nazwa kategorii jest pusta. Warstwa HTTP tłumaczy to na 400.</summary>
public sealed class CategoryNameRequiredException : Exception;
