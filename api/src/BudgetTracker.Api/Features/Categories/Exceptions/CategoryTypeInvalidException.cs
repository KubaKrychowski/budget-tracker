namespace BudgetTracker.Api.Features.Categories.Exceptions;

/// <summary>Typ kategorii nie jest ani wydatkiem, ani wpływem. Warstwa HTTP tłumaczy to na 400.</summary>
public sealed class CategoryTypeInvalidException : Exception;
