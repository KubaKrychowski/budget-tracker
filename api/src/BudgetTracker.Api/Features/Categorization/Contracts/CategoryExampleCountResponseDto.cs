namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Liczba przykładów jednej kategorii w zbiorze treningowym.</summary>
/// <param name="Count">
/// Liczba przykładów w zbiorze. <c>0</c> jest tu pełnoprawną wartością, nie brakiem danych:
/// kategorii, której model nigdy nie widział, nigdy też nie wskaże — i to trzeba pokazać.
/// </param>
public sealed record CategoryExampleCountResponseDto(string Name, int Count);
