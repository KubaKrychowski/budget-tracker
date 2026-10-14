namespace BudgetTracker.Api.Features.ProductCategories.Exceptions;

/// <summary>Nazwa kategorii produktu przekracza limit znaków. Warstwa HTTP tłumaczy to na 400.</summary>
public sealed class ProductCategoryNameTooLongException : Exception;
