namespace BudgetTracker.Api.Features.ProductCategories.Exceptions;

/// <summary>Kategoria produktu wskazana w CIELE żądania nie istnieje. Warstwa HTTP tłumaczy to na 400, nie 404 — to złe wejście, nie zły adres.</summary>
public sealed class ProductCategoryInvalidException : Exception;
