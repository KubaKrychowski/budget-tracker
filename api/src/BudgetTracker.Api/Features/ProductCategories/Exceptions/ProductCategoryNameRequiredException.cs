namespace BudgetTracker.Api.Features.ProductCategories.Exceptions;

/// <summary>Nazwa kategorii produktu jest pusta po przycięciu. Warstwa HTTP tłumaczy to na 400.</summary>
public sealed class ProductCategoryNameRequiredException : Exception;
