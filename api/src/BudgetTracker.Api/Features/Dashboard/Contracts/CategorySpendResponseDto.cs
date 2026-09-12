namespace BudgetTracker.Api.Features.Dashboard.Contracts;

/// <summary>Wydatki jednej kategorii w oglądanym okresie, jako wartość dodatnia.</summary>
/// <param name="CategoryId">
/// <c>null</c> dla kubełka „bez kategorii" — sentinel, nie przetłumaczony tekst. Front filtruje
/// listę transakcji po tym polu, nigdy po <paramref name="CategoryName"/>: nazwa jest wyłącznie
/// etykietą osi wykresu i zmienia się z językiem.
/// </param>
public record CategorySpendResponseDto(Guid? CategoryId, string CategoryName, decimal Amount);
