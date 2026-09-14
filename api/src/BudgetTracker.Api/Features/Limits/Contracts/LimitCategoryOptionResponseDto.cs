namespace BudgetTracker.Api.Features.Limits.Contracts;

/// <summary>Kategoria do wyboru w modalu „Ustaw limit".</summary>
/// <param name="HasLimit">
/// Czy kategoria ma już limit w oglądanym miesiącu. Modal dodawania jej nie pokazuje — do zmiany kwoty
/// służy „Zmień" w wierszu tabeli, żeby jedna kategoria nie dostała dwóch limitów na raz przez pomyłkę.
/// </param>
public sealed record LimitCategoryOptionResponseDto(Guid Id, string Name, bool HasLimit);
